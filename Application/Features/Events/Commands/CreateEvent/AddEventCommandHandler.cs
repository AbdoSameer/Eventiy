using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using Domain.Primitives;

namespace Application.Features.Events.Commands.CreateEvent
{
    internal sealed class AddEventCommandHandler : ICommandHandler<AddEventCommand, Guid>
    {
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AddEventCommandHandler(
            IEventRepository eventRepository,
            IUnitOfWork unitOfWork)
        {
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            AddEventCommand request,
            CancellationToken cancellationToken)
        {
            var addressResult = Address.Create(request.Country, request.City, request.Street);
            if (addressResult.IsFailure)
            {
                return Result<Guid>.Failure(addressResult.Error);
            }

            var eventResult = Event.Create(
                request.Name,
                request.Date,
                addressResult.Value,
                request.Capacity,
                request.Description);

            if (eventResult.IsFailure)
            {
                return Result<Guid>.Failure(eventResult.Error);
            }

            var addResult = await _eventRepository.AddAsync(eventResult.Value, cancellationToken);
            if (addResult.IsFailure)
            {
                return Result<Guid>.Failure(addResult.Error);
            }

            var commitResult = await _unitOfWork.CommitAsync(cancellationToken);
            if (commitResult.IsFailure)
            {
                return Result<Guid>.Failure(commitResult.Error);
            }

            return Result<Guid>.Success(eventResult.Value.Id.Value);
        }
    }
}
