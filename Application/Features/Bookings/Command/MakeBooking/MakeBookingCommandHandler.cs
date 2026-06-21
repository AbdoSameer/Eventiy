using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Errors;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

namespace Application.Features.Bookings.Command.MakeBooking
{
    public class MakeBookingCommandHandler : ICommandHandler<MakeBookingCommand, Guid>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;

        public MakeBookingCommandHandler(IBookingRepository bookingRepository,
                                         IUnitOfWork unitOfWork,
                                         IEventRepository eventRepository)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
        }

        public async Task<Result<Guid>> Handle(
            MakeBookingCommand request,
            CancellationToken cancellationToken)
        {
            var eventIdResult = EventId.Create(request.EventId);

            if (eventIdResult.IsFailure)
                return Result<Guid>.Failure(eventIdResult.Error);

            var @event = await _eventRepository.GetByIdAsync(
                eventIdResult.Value,
                cancellationToken);

            if (@event is null)
                return Result<Guid>.Failure("Event not found");


            var validEventdate = @event.Date;
            if(validEventdate < DateTime.Now)
                return Result<Guid>.Failure("Event has passed");

            var reservedQuantity = await _bookingRepository
                                        .GetTotalReservedAsync(@event.Id,
                                                               cancellationToken);
       
            if (reservedQuantity + request.Quantity > @event.Capacity)
                return Result<Guid>.Failure(
                    BookingErrors.InsufficientSeats(
                        request.Quantity,
                        @event.Capacity - reservedQuantity));

            var validUser = UserId.Create(Guid.NewGuid());

            if (validUser.IsFailure)
                return Result<Guid>.Failure(validUser.Error);

            var validTicketType = TicketTypeId.Create(request.TicketTypeId);
            if (validTicketType.IsFailure)
                return Result<Guid>.Failure(validTicketType.Error);

            var ticketType= @event.TicketTypes.FirstOrDefault(x=> x.Id == validTicketType.Value );
            var EventTitle = @event.Name.Value;

            var @booking = Booking.Create(
                validUser.Value,
                @event.Id,
                validTicketType.Value,
                EventTitle,
                request.Quantity,
                ticketType!.Price);
                
            if (@booking.IsFailure)
                return Result<Guid>.Failure(@booking.Error);
            
            await _bookingRepository.AddBookingAsync(@booking.Value,cancellationToken);
            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0 )
                return Result<Guid>.Failure(" Failed to make booking ");
        
            return Result<Guid>.Success(@booking.Value.Id.Value);
        }
    }
}
