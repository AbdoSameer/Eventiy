using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.Features.Bookings.Events.BookingCancelled;

public class BookingCancelledEventHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly ILogger<BookingCancelledEventHandler> _logger;

    public BookingCancelledEventHandler(ILogger<BookingCancelledEventHandler> logger)
    {
        _logger = logger;
    }

    public Task<Result> HandleAsync(
        BookingCancelledEvent notification,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Booking {BookingId} cancelled — seat release already handled by command handler",
            notification.BookingId);

        return Task.FromResult(Result.Success());
    }
}
