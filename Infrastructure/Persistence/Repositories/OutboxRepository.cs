// Infrastructure/Persistence/Repositories/OutboxRepository.cs
using Application.Abstractions.Outbox;
using Infrastructure.Persistence.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// ✅ Implementation of IOutboxRepository
/// ✅ Converts between OutboxMessageDto (Application) and OutboxMessage (Infrastructure)
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _context;

    public OutboxRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // ==================== ADD (ChangeTracker Only) ====================

    public void Add(OutboxMessageDto messageDto)
    {
        // ✅ Convert DTO to Infrastructure Entity
        var message = new OutboxMessage(
            messageDto.Id,
            messageDto.EventName,
            messageDto.Domain,
            messageDto.Payload,
            messageDto.OccurredOnUtc,
            messageDto.ProcessedOnUtc,
            messageDto.NextRetryOnUtc,
            messageDto.Error,
            messageDto.RetryCount);

        _context.OutboxMessages.Add(message);
    }

    public void AddRange(IEnumerable<OutboxMessageDto> messageDtos)
    {
        // ✅ Convert DTOs to Infrastructure Entities
        var messages = messageDtos.Select(dto => new OutboxMessage(
            dto.Id,
            dto.EventName,
            dto.Domain,
            dto.Payload,
            dto.OccurredOnUtc,
            dto.ProcessedOnUtc,
            dto.NextRetryOnUtc,
            dto.Error,
            dto.RetryCount));

        _context.OutboxMessages.AddRange(messages);
    }

    // ==================== LOCK & GET ====================

    public async Task<IReadOnlyList<OutboxMessageDto>> GetAndLockUnprocessedMessagesAsync(
        Guid lockId,
        int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            UPDATE TOP (@BatchSize) OutboxMessages
            SET 
                ProcessingLock = @LockId,
                ProcessingLockedAt = GETUTCDATE()
            OUTPUT INSERTED.*
            WHERE 
                IsProcessed = 0 
                AND (NextRetryOnUtc IS NULL OR NextRetryOnUtc <= GETUTCDATE())
                AND (ProcessingLock IS NULL OR 
                     ProcessingLockedAt <= DATEADD(MINUTE, -5, GETUTCDATE()))
            ORDER BY OccurredOnUtc";

        var parameters = new[]
        {
            new SqlParameter("@LockId", lockId),
            new SqlParameter("@BatchSize", batchSize)
        };

        // ✅ Get Infrastructure Entities
        var messages = await _context.OutboxMessages
            .FromSqlRaw(sql, parameters)
            .ToListAsync(cancellationToken);

        // ✅ Convert to DTOs
        return messages.Select(m => new OutboxMessageDto(
            m.Id,
            m.EventName,
            m.Domain,
            m.Payload,
            m.OccurredOnUtc,
            m.ProcessedOnUtc,
            m.NextRetryOnUtc,
            m.Error,
            m.RetryCount,
            m.IsProcessed,
            m.IsReadyForProcessing)).ToList();
    }

    // ==================== UPDATE (Individual) ====================

    public async Task MarkAsProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FindAsync(new object[] { id }, cancellationToken);

        if (message is null) return;

        message.MarkAsProcessed();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages
            .FindAsync(new object[] { id }, cancellationToken);

        if (message is null) return;

        message.MarkAsFailed(error);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ==================== BATCH UPDATE ====================

    public void MarkRangeAsProcessed(IEnumerable<Guid> messageIds)
    {
        var ids = messageIds.ToList();
        if (!ids.Any()) return;

        foreach (var id in ids)
        {
            var message = _context.OutboxMessages.Local
                .FirstOrDefault(m => m.Id == id);

            if (message != null)
            {
                message.MarkAsProcessed();
            }
        }
    }

    public void MarkRangeAsFailed(IEnumerable<(Guid Id, string Error)> failedMessages)
    {
        var failedList = failedMessages.ToList();
        if (!failedList.Any()) return;

        foreach (var (id, error) in failedList)
        {
            var message = _context.OutboxMessages.Local
                .FirstOrDefault(m => m.Id == id);

            if (message != null)
            {
                message.MarkAsFailed(error);
            }
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ==================== QUERY ====================

    public async Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => !m.IsProcessed && m.IsReadyForProcessing)
            .CountAsync(cancellationToken);
    }

    // ==================== LOCK RELEASE ====================

    public async Task ReleaseLockAsync(Guid lockId, CancellationToken cancellationToken = default)
    {
        var sql = @"
            UPDATE OutboxMessages
            SET 
                ProcessingLock = NULL,
                ProcessingLockedAt = NULL
            WHERE ProcessingLock = @LockId";

        await _context.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@LockId", lockId));
    }
}