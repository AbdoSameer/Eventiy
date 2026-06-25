
namespace Application.Abstractions.Outbox
{
    public interface IOutboxProcessor
    {
        Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
    }
}
