using Application.Abstractions.Payments;
using Infrastructure.Persistence.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class CompensationLogRepository : ICompensationLogRepository
{
    private readonly ApplicationDbContext _context;

    public CompensationLogRepository(ApplicationDbContext context)
        => _context = context;

    public void Add(CompensationLogDto dto)
    {
        var entity = new CompensationLog(
            id: dto.Id,
            bookingId: dto.BookingId,
            compensationType: dto.CompensationType,
            payload: dto.Payload,
            occurredOnUtc: dto.OccurredOnUtc,
            idempotencyKey: dto.IdempotencyKey);

        _context.CompensationLogs.Add(entity);
    }

    public void AddRange(IEnumerable<CompensationLogDto> dtos)
    {
        var entities = dtos.Select(dto => new CompensationLog(
            id: dto.Id,
            bookingId: dto.BookingId,
            compensationType: dto.CompensationType,
            payload: dto.Payload,
            occurredOnUtc: dto.OccurredOnUtc,
            idempotencyKey: dto.IdempotencyKey));

        _context.CompensationLogs.AddRange(entities);
    }

    public async Task<IReadOnlyList<CompensationLogDto>> GetAndLockUnprocessedAsync(
        Guid lockId,
        TimeProvider timeProvider,
        int batchSize = 50,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var sql = @"
            UPDATE TOP (@BatchSize) CompensationLogs WITH (UPDLOCK, READPAST, ROWLOCK)
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

        var logs = await _context.CompensationLogs
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(ct);

        return logs.Select(l => new CompensationLogDto(
            Id: l.Id,
            BookingId: l.BookingId,
            CompensationType: l.CompensationType,
            Payload: l.Payload,
            OccurredOnUtc: l.OccurredOnUtc,
            IdempotencyKey: l.IdempotencyKey,
            ProcessedOnUtc: l.ProcessedOnUtc,
            Error: l.Error,
            RetryCount: l.RetryCount,
            NextRetryOnUtc: l.NextRetryOnUtc)).ToList();
    }

    public async Task MarkRangeAsProcessedAsync(
        IEnumerable<Guid> ids,
        DateTime processedAt,
        CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;

        await _context.CompensationLogs
            .Where(c => idList.Contains(c.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ProcessedOnUtc, processedAt)
                .SetProperty(c => c.ProcessingLock, (Guid?)null)
                .SetProperty(c => c.ProcessingLockedAt, (DateTime?)null),
            ct);
    }

    public async Task MarkRangeAsFailedAsync(
        IEnumerable<(Guid Id, string Error, int NewRetryCount, DateTime? NextRetryOnUtc)> failed,
        CancellationToken ct = default)
    {
        var failList = failed.ToList();
        if (failList.Count == 0) return;

        foreach (var failInfo in failList)
        {
            await _context.CompensationLogs
                .Where(c => c.Id == failInfo.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Error, failInfo.Error)
                    .SetProperty(c => c.RetryCount, failInfo.NewRetryCount)
                    .SetProperty(c => c.NextRetryOnUtc, failInfo.NextRetryOnUtc)
                    .SetProperty(c => c.ProcessingLock, (Guid?)null)
                    .SetProperty(c => c.ProcessingLockedAt, (DateTime?)null),
                ct);
        }
    }

    public async Task ReleaseLockAsync(Guid lockId, CancellationToken ct = default)
    {
        // IMPORTANT: use the (string, IEnumerable<object>, CancellationToken)
        // overload explicitly. Passing the SqlParameter and CancellationToken as
        // positional args hits the (string, params object[]) overload, which
        // treats the CancellationToken itself as a SQL parameter and throws
        // "no store type mapping for CancellationToken".
        await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE CompensationLogs
              SET ProcessingLock = NULL, ProcessingLockedAt = NULL
              WHERE ProcessingLock = @LockId",
            parameters: new object[] { new SqlParameter("@LockId", lockId) },
            cancellationToken: ct);
    }

    public async Task MoveToDeadLetterAsync(
        Guid compensationId,
        string failedReason,
        DateTime movedAt,
        CancellationToken ct = default)
    {
        var log = await _context.CompensationLogs
            .FirstOrDefaultAsync(c => c.Id == compensationId, ct);

        if (log is null) return;

        var deadLetter = new OutboxDeadLetter
        {
            Id = log.Id,
            EventName = log.CompensationType,
            Domain = "Compensation",
            Payload = log.Payload,
            OccurredOnUtc = log.OccurredOnUtc,
            IdempotencyKey = log.IdempotencyKey,
            RetryCount = log.RetryCount,
            FailedReason = failedReason,
            MovedToDeadLetterAt = movedAt,
        };

        _context.OutboxDeadLetters.Add(deadLetter);
        _context.CompensationLogs.Remove(log);
    }
}
