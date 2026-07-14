using Domain.Common;

namespace Application.Abstractions.Outbox;

public sealed record OutboxDispatchResult(
    bool IsSuccess,
    IReadOnlyList<Guid> ProcessedIds,
    IReadOnlyList<OutboxFailedMessageUpdateDto> FailedMessages);

public interface IOutboxDispatcher
{
    Task<OutboxDispatchResult> DispatchBatchAsync(
        IReadOnlyList<OutboxMessageDto> messages,
        Guid lockId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);
}
