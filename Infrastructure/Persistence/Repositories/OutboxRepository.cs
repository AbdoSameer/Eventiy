using Application.Abstractions.Outbox;
using Domain.Common;
using Infrastructure.Persistence.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _context;

    public OutboxRepository(ApplicationDbContext context)
        => _context = context;

    public void Add(OutboxMessageDto dto)
    {
        var message = new OutboxMessage(
            id: dto.Id,
            eventName: dto.EventName,
            domain: dto.Domain,
            payload: dto.Payload,
            occurredOnUtc: dto.OccurredOnUtc,
            idempotencyKey: dto.IdempotencyKey,
            processedOnUtc: dto.ProcessedOnUtc,
            nextRetryOnUtc: dto.NextRetryOnUtc,
            error: dto.Error,
            retryCount: dto.RetryCount);

        _context.OutboxMessages.Add(message);
    }

    public void AddRange(IEnumerable<OutboxMessageDto> dtos)
    {
        var entities = dtos.Select(dto => new OutboxMessage(
            id: dto.Id,
            eventName: dto.EventName,
            domain: dto.Domain,
            payload: dto.Payload,
            occurredOnUtc: dto.OccurredOnUtc,
            idempotencyKey: dto.IdempotencyKey,
            processedOnUtc: dto.ProcessedOnUtc,
            nextRetryOnUtc: dto.NextRetryOnUtc,
            error: dto.Error,
            retryCount: dto.RetryCount));

        _context.OutboxMessages.AddRange(entities);
    }

    public async Task<IReadOnlyList<OutboxMessageDto>> GetAndLockUnprocessedMessagesAsync(
        Guid lockId,
        TimeProvider dateTimeProvider,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.GetUtcNow().UtcDateTime;

        var sql = @"
            UPDATE TOP (@BatchSize) OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
            SET 
                ProcessingLock     = @LockId,
                ProcessingLockedAt = @Now
            OUTPUT INSERTED.*
            WHERE 
                ProcessedOnUtc IS NULL
                AND (NextRetryOnUtc IS NULL OR NextRetryOnUtc <= @Now)
                AND (ProcessingLock IS NULL OR 
                     ProcessingLockedAt <= DATEADD(MINUTE, -5, @Now))";

        var parameters = new[]
        {
            new SqlParameter("@LockId", lockId),
            new SqlParameter("@BatchSize", batchSize),
            new SqlParameter("@Now", now)
        };

        var messages = await _context.OutboxMessages
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages.Select(m => new OutboxMessageDto(
            Id: m.Id,
            EventName: m.EventName,
            Domain: m.Domain,
            Payload: m.Payload,
            OccurredOnUtc: m.OccurredOnUtc,
            IdempotencyKey: m.IdempotencyKey,
            ProcessedOnUtc: m.ProcessedOnUtc,
            NextRetryOnUtc: m.NextRetryOnUtc,
            Error: m.Error,
            RetryCount: m.RetryCount)).ToList();
    }

    public async Task MarkRangeAsProcessedAsync(
        IEnumerable<Guid> ids,
        DateTime processedOnUtc,
        CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any()) return;

        await _context.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessedOnUtc, processedOnUtc)
                .SetProperty(m => m.ProcessingLock, (Guid?)null)
                .SetProperty(m => m.ProcessingLockedAt, (DateTime?)null),
            cancellationToken);
    }

    public async Task MarkRangeAsFailedAsync(
        IEnumerable<OutboxFailedMessageUpdateDto> failedMessages,
        CancellationToken cancellationToken = default)
    {
        if (failedMessages == null || !failedMessages.Any()) return;

        foreach (var failInfo in failedMessages)
        {
            await _context.OutboxMessages
                .Where(m => m.Id == failInfo.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Error, failInfo.Error)
                    .SetProperty(m => m.RetryCount, failInfo.NewRetryCount)
                    .SetProperty(m => m.NextRetryOnUtc, failInfo.NextRetryOnUtc)
                    .SetProperty(m => m.ProcessingLock, (Guid?)null)
                    .SetProperty(m => m.ProcessingLockedAt, (DateTime?)null),
                cancellationToken);
        }
    }

    public async Task ReleaseLockAsync(
        Guid lockId,
        CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE OutboxMessages
              SET ProcessingLock = NULL, ProcessingLockedAt = NULL
              WHERE ProcessingLock = @LockId",
            new SqlParameter("@LockId", lockId),
            cancellationToken);
    }

    public async Task<int> GetUnprocessedCountAsync(
        DateTime currentTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m =>
                m.ProcessedOnUtc == null &&
                (m.NextRetryOnUtc == null || m.NextRetryOnUtc <= currentTime))
            .CountAsync(cancellationToken);
    }

    public async Task MoveToDeadLetterAsync(
        Guid messageId,
        string failedReason,
        DateTime movedAt,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null) return;

        var deadLetter = new OutboxDeadLetter
        {
            Id = message.Id,
            EventName = message.EventName,
            Domain = message.Domain,
            Payload = message.Payload,
            OccurredOnUtc = message.OccurredOnUtc,
            IdempotencyKey = message.IdempotencyKey,
            RetryCount = message.RetryCount,
            FailedReason = failedReason,
            MovedToDeadLetterAt = movedAt,
        };

        _context.OutboxDeadLetters.Add(deadLetter);
        _context.OutboxMessages.Remove(message);
    }

    public async Task<IReadOnlyList<DeadLetterDto>> GetDeadLettersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.OutboxDeadLetters
            .OrderByDescending(d => d.MovedToDeadLetterAt)
            .Select(d => new DeadLetterDto(
                d.Id, d.EventName, d.Domain, d.Payload,
                d.OccurredOnUtc, d.IdempotencyKey,
                d.RetryCount, d.FailedReason, d.MovedToDeadLetterAt))
            .ToListAsync(cancellationToken);
    }

    public async Task RequeueDeadLetterAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deadLetter = await _context.OutboxDeadLetters
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (deadLetter is null) return;

        var message = new OutboxMessage(
            id: deadLetter.Id,
            eventName: deadLetter.EventName,
            domain: deadLetter.Domain,
            payload: deadLetter.Payload,
            occurredOnUtc: deadLetter.OccurredOnUtc,
            idempotencyKey: deadLetter.IdempotencyKey,
            processedOnUtc: null,
            nextRetryOnUtc: null,
            error: null,
            retryCount: 0);

        _context.OutboxMessages.Add(message);
        _context.OutboxDeadLetters.Remove(deadLetter);
    }
}
