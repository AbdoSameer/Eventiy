using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using Domain.Primitives;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;


namespace Application.Features.Events.Commands.CreateEvent
{
    internal class CreateEventCommandHandler
                        : ICommandHandler<CreateEventCommand, Guid>
    {
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CreateEventCommandHandler(IEventRepository eventRepository, IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
        {
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
        }
        public async Task<Result<Guid>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            var addressResult = Address.Create(
                 request.Location.Country,
                 request.Location.City,
                 request.Location.Street);
            
            if (addressResult.IsFailure)
            {
                return Result<Guid>.Failure(addressResult.Errors.ToArray());
            }

            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

            var @event = Event
                        .Create(request.Name,
                                request.Capacity,
                                request.Date,
                                addressResult.Value,
                                request.Description,
                                _dateTimeProvider,
                                metadata);

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

            return Result<Guid>.Success(@event.Value.Id.Value);

        }
    }
}
