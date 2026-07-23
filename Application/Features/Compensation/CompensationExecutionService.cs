using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.Features.Compensation;

public sealed class CompensationExecutionService : ICompensationExecutionService
{
    private readonly IPaymentService _paymentService;
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CompensationExecutionService> _logger;

    public CompensationExecutionService(
        IPaymentService paymentService,
        IBookingRepository bookingRepo,
        IEventRepository eventRepo,
        IUnitOfWork uow,
        ILogger<CompensationExecutionService> logger)
    {
        _paymentService = paymentService;
        _bookingRepo = bookingRepo;
        _eventRepo = eventRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(CompensationLogDto log, CancellationToken ct)
    {
        if (log.CompensationType == "CancelPayment")
            return await _paymentService.CancelPaymentAsync(log.BookingId, ct);

        if (log.CompensationType == "CompensateOversoldBooking")
        {
            var bookingIdResult = BookingId.Create(log.BookingId);
            if (bookingIdResult.IsFailure)
                return Result.Failure(bookingIdResult.Errors.ToArray());

            var booking = await _bookingRepo.GetByIdAsync(bookingIdResult.Value, ct);
            if (booking is null)
                return Result.Success();

            if (booking.Status != BookingStatusEnum.Pending)
                return Result.Success();

            var utcNow = DateTime.UtcNow;

            var cancelResult = booking.Cancel(utcNow, "Oversold compensation");
            if (cancelResult.IsFailure)
                return cancelResult;

            var evt = await _eventRepo.GetByIdAsync(booking.EventId, ct);
            if (evt is null)
                return Result.Success();

            var releaseResult = evt.ReleaseSeats(booking.TicketTypeId, booking.Quantity, utcNow);
            if (releaseResult.IsFailure)
                return releaseResult;

            await _uow.CommitAsync(ct);

            _logger.LogInformation(
                "Booking {BookingId} cancelled and seats released due to oversold compensation",
                log.BookingId);

            return Result.Success();
        }

        return Result.Failure(Error.Failure(
            "Compensation.UnknownType",
            $"Unknown compensation type: {log.CompensationType}"));
    }
}
