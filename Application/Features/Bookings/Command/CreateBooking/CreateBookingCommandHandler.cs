using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, BookingId>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;

        public CreateBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository
                                           )
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
        }

        public async Task<Result<BookingId>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            // ===== 1. Validate and Create Value Objects ===================

            var UserIdResult = UserId.Create(Guid.NewGuid());
            if (UserIdResult.IsFailure)
            {
                return Result<BookingId>.Failure(UserIdResult.Errors.ToArray());
            }

            var ticketTypeIdResult = TicketTypeId.Create(request.TicketTypeId);
            if (ticketTypeIdResult.IsFailure)
            {
                return Result<BookingId>.Failure(ticketTypeIdResult.Errors.ToArray());
            }   

            var eventIdResult =EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
            {
                return Result<BookingId>.Failure(eventIdResult.Errors.ToArray());
            }

            // ===== 2. Get Event from Repository ==========================

            var eventResult = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
            if (eventResult is null )
            {
                return Result<BookingId>.Failure(EventErrors.EventNotFound(eventIdResult.Value));
            }

            // ===== 3. Get and Validate TicketType ========================
            var TicketTypeResult = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeIdResult.Value);
            if (TicketTypeResult is null)
            {
                return Result<BookingId>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));
            }

            if (TicketTypeResult.AvailableCount < request.Quantity)
            {
                return Result<BookingId>.Failure(
                    BookingErrors.InsufficientSeats(request.Quantity, TicketTypeResult.AvailableCount));
            }


            var booking = Booking.Create(UserIdResult.Value,
                                         eventIdResult.Value,
                                         ticketTypeIdResult.Value,
                                         eventResult.EventName.Value,
                                         request.Quantity,
                                         TicketTypeResult.Price);
            if (booking.IsFailure)
            {
                return Result<BookingId>.Failure(booking.Errors.ToArray());
            }

            var reservationResult = eventResult.ReserveSeats(ticketTypeIdResult.Value, request.Quantity); 
            if (reservationResult.IsFailure)
            {
                return Result<BookingId>.Failure(reservationResult.Errors.ToArray());
            }

            await _bookingRepository.AddBookingAsync(booking.Value, cancellationToken);

            var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
            {
                return Result<BookingId>.Failure(BookingErrors.BookingCreationFailed());
            }

            return Result<BookingId>.Success(booking.Value.Id);

        }
    }
}
