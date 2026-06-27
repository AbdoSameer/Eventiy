using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, BookingId>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CreateBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository,
                                           IDateTimeProvider dateTimeProvider)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
        }
        public async Task<Result<BookingId>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            // ===== 1. Validate and Create Value Objects ===================
            var UserIdResult = UserId.Create(Guid.NewGuid());
            if (UserIdResult.IsFailure)
                return Result<BookingId>.Failure(UserIdResult.Errors.ToArray());

            var ticketTypeIdResult = TicketTypeId.Create(request.TicketTypeId);
            if (ticketTypeIdResult.IsFailure)
                return Result<BookingId>.Failure(ticketTypeIdResult.Errors.ToArray());

            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result<BookingId>.Failure(eventIdResult.Errors.ToArray());

            // ===== 2. Get Event from Repository ==========================
            var eventResult = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
            if (eventResult is null)
                return Result<BookingId>.Failure(EventErrors.EventNotFound(eventIdResult.Value));

            // ===== 3. Get TicketType Validation ========================
            var ticketType = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeIdResult.Value);
            if (ticketType is null)
                return Result<BookingId>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));

            // ===== 4. Execute Capacity Reservation via Aggregate Root =====
            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);
            
            var reservationResult = eventResult
                                        .ReserveSeats(ticketTypeIdResult.Value,
                                                      request.Quantity,
                                                      _dateTimeProvider,
                                                      metadata);
            if (reservationResult.IsFailure)
            {
                return Result<BookingId>.Failure(reservationResult.Errors.ToArray());
            }

            // ===== 5. Create Booking Entity =============================
            var booking = Booking.Create(
                UserIdResult.Value,
                eventIdResult.Value,
                ticketTypeIdResult.Value,
                eventResult.EventName.Value,
                request.Quantity,
                ticketType.Price,
                _dateTimeProvider,
                metadata);

            if (booking.IsFailure)
                return Result<BookingId>.Failure(booking.Errors.ToArray());

            // ===== 6. Persist within the same Unit of Work Transaction ====
            await _bookingRepository.AddBookingAsync(booking.Value, cancellationToken);

            try
            {
                var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);
                if (rowsAffected <= 0)
                {
                    return Result<BookingId>.Failure(BookingErrors.BookingCreationFailed());
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<BookingId>.Failure(BookingErrors.ConcurrencyConflict());
            }

            return Result<BookingId>.Success(booking.Value.Id);
        }
    }
}
