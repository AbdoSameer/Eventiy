using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using Domain.Primitives;


namespace Application.Features.Events.Commands.CreateEvent
{
    internal class CreateEventCommandHandler
                        : ICommandHandler<CreateEventCommand, Guid>
    {
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateEventCommandHandler(IEventRepository eventRepository, IUnitOfWork unitOfWork)
        {
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
        }
        public async Task<Result<Guid>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            var addressResult = Address.Create(
                 request.Location.Country,
                 request.Location.City,
                 request.Location.Street);
            
            if (addressResult.IsFailure)
            {
                return Result<Guid>.Failure(addressResult.Error);
            }

            var @event = Event
                        .Create(request.Name,
                                request.Capacity,
                                request.Date,
                                addressResult.Value,
                                request.Description);

            if (@event.IsFailure)
            {
                return Result<Guid>.Failure(@event.Error);
            }
            
            var addResult = await _eventRepository.AddAsync(@event.Value, cancellationToken);
            if (addResult.IsFailure)
            {
                return Result<Guid>.Failure(addResult.Error);
            }
        

            var commitResult = await _unitOfWork.CommitAsync(cancellationToken);
            if (commitResult.IsFailure)
            {
                return Result<Guid>.Failure(commitResult.Error);
            }

            return Result<Guid>.Success(@event.Value.Id.Value);

        }
    }
}
