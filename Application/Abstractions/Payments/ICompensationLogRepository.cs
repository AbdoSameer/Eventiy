using Application.Abstractions.Payments;

namespace Application.Abstractions.Payments;

public interface ICompensationLogRepository
{
    void Add(CompensationLogDto log);
    void AddRange(IEnumerable<CompensationLogDto> logs);

    Task<IReadOnlyList<CompensationLogDto>> GetAndLockUnprocessedAsync(
        Guid lockId,
        TimeProvider timeProvider,
        int batchSize = 50,
        CancellationToken ct = default);

    Task MarkRangeAsProcessedAsync(
        IEnumerable<Guid> ids,
        DateTime processedAt,
        CancellationToken ct = default);

    Task MarkRangeAsFailedAsync(
        IEnumerable<(Guid Id, string Error, int NewRetryCount, DateTime? NextRetryOnUtc)> failed,
        CancellationToken ct = default);

    Task ReleaseLockAsync(Guid lockId, CancellationToken ct = default);

    Task MoveToDeadLetterAsync(
        Guid compensationId,
        string failedReason,
        DateTime movedAt,
        CancellationToken ct = default);
}
