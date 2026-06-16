using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;


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
            var @event = Event
                        .Create(request.Name,
                                request.Capacity,
                                request.Date,
                                request.Location,
                                request.Description);

            if (@event.IsFailure)
            {
                return Result<Guid>.Failure(@event.Error);
            }
            
            await _eventRepository.AddEventAsync(@event.Value, cancellationToken);
        

            await _unitOfWork.CommitAsync(cancellationToken);

            return Result<Guid>.Success(@event.Value.Id.Value);

        }
    }
}
