using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, BookingId>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;

        public CreateBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository,
                                           IDateTimeProvider dateTimeProvider,
                                           ICurrentUserService currentUserService)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
        }
        public async Task<Result<BookingId>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var userIdResult = _currentUserService.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Result<BookingId>.Failure(userIdResult.Errors.ToArray());

            var ticketTypeIdResult = TicketTypeId.Create(request.TicketTypeId);
            if (ticketTypeIdResult.IsFailure)
                return Result<BookingId>.Failure(ticketTypeIdResult.Errors.ToArray());

            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result<BookingId>.Failure(eventIdResult.Errors.ToArray());

            var eventResult = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
            if (eventResult is null)
                return Result<BookingId>.Failure(EventErrors.EventNotFound(eventIdResult.Value));

            var ticketType = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeIdResult.Value);
            if (ticketType is null)
                return Result<BookingId>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));

            var metadata = EventMetadata.Create(Guid.NewGuid().ToString(), null, null);
            
            var reservationResult = eventResult
                                        .ReserveSeats(ticketTypeIdResult.Value,
                                                      request.Quantity,
                                                      _dateTimeProvider,
                                                      metadata);
            if (reservationResult.IsFailure)
            {
                return Result<BookingId>.Failure(reservationResult.Errors.ToArray());
            }

            var booking = Booking.Create(
                userIdResult.Value,
                eventIdResult.Value,
                ticketTypeIdResult.Value,
                eventResult.EventName.Value,
                request.Quantity,
                ticketType.Price,
                _dateTimeProvider,
                metadata);

            if (booking.IsFailure)
                return Result<BookingId>.Failure(booking.Errors.ToArray());

            await _bookingRepository.AddBookingAsync(booking.Value, cancellationToken);

           
            return Result<BookingId>.Success(booking.Value.Id);
        }
    }
}
