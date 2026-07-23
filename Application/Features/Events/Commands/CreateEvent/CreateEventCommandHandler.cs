using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using Domain.Primitives;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Events.Commands.CreateEvent
{
    internal class CreateEventCommandHandler
                        : ICommandHandler<CreateEventCommand, Guid>
    {
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICacheService _cache;

        public CreateEventCommandHandler(
            IEventRepository eventRepository,
            IUnitOfWork unitOfWork,
            TimeProvider dateTimeProvider,
            ICacheService cache)
        {
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
            _cache = cache;
        }

        public async Task<Result<Guid>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            var addressResult = Address.Create(
                 request.Location.Country,
                 request.Location.City,
                 request.Location.Street,
                 latitude: request.Latitude ?? request.Location.Latitude,
                 longitude: request.Longitude ?? request.Location.Longitude);

            if (addressResult.IsFailure)
            {
                return Result<Guid>.Failure(addressResult.Errors.ToArray());
            }

            var @event = Event
                        .Create(request.Name,
                                request.Capacity,
                                request.Date,
                                addressResult.Value,
                                request.Description,
                                request.Type,
                                _dateTimeProvider.GetUtcNow().UtcDateTime);

            if (@event.IsFailure)
            {
                return Result<Guid>.Failure(@event.Errors.ToArray());
            }

            await _eventRepository.AddEventAsync(@event.Value, cancellationToken);

            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0)
            {
                return Result<Guid>.Failure(
                    Error.Failure(
                        "EventCreationFailed",
                        "Failed to create the event. Please try again later."));
            }

            await _cache.RemoveByPatternAsync(EventsListPattern, cancellationToken);

            return Result<Guid>.Success(@event.Value.Id.Value);
        }
    }
}
