using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, Guid>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEventMetadataFactory _metadataFactory;
        private readonly ICacheService _cache;

        public CreateBookingCommandHandler(
            IBookingRepository bookingRepository,
            IUnitOfWork unitOfWork,
            IEventRepository eventRepository,
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            IEventMetadataFactory metadataFactory,
            ICacheService cache)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _metadataFactory = metadataFactory;
            _cache = cache;
        }
        private const int MAX_CONCURRENCY_RETRIES = 3;

        public async Task<Result<Guid>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var userIdResult = _currentUserService.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Result<Guid>.Failure(userIdResult.Errors.ToArray());

            var ticketTypeIdResult = TicketTypeId.Create(request.TicketTypeId);
            if (ticketTypeIdResult.IsFailure)
                return Result<Guid>.Failure(ticketTypeIdResult.Errors.ToArray());

            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result<Guid>.Failure(eventIdResult.Errors.ToArray());

            for (var attempt = 1; attempt <= MAX_CONCURRENCY_RETRIES; attempt++)
            {
                try
                {
                    return await AttemptBooking(
                        userIdResult.Value,
                        eventIdResult.Value,
                        ticketTypeIdResult.Value,
                        request,
                        cancellationToken);
                }
                catch (ConcurrencyException) when (attempt < MAX_CONCURRENCY_RETRIES)
                {
                    // Re-read fresh data and retry
                }
            }

            return Result<Guid>.Failure(BookingErrors.ConcurrencyConflict());
        }

        private async Task<Result<Guid>> AttemptBooking(
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            CreateBookingCommand request,
            CancellationToken cancellationToken)
        {
            var eventResult = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
            if (eventResult is null)
                return Result<Guid>.Failure(EventErrors.EventNotFound(eventId));

            var ticketType = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result<Guid>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));

            var metadata = _metadataFactory.Create();

            var reservationResult = eventResult.ReserveSeats(
                ticketTypeId, request.Quantity, _dateTimeProvider.GetUtcNow().UtcDateTime, metadata);
            if (reservationResult.IsFailure)
                return Result<Guid>.Failure(reservationResult.Errors.ToArray());

            var booking = Booking.Create(
                userId, eventId, ticketTypeId,
                eventResult.EventName.Value,
                request.Quantity,
                ticketType.Price,
                request.PaymentMethod,
                _dateTimeProvider.GetUtcNow().UtcDateTime,
                metadata);

            if (booking.IsFailure)
                return Result<Guid>.Failure(booking.Errors.ToArray());

            await _bookingRepository.AddBookingAsync(booking.Value, cancellationToken);

            var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
                return Result<Guid>.Failure(BookingErrors.BookingCreationFailed());

            await _cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);

            return Result<Guid>.Success(booking.Value.Id.Value);
        }
    }
}
