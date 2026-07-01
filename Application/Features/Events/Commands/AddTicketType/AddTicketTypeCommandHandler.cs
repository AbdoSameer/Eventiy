using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;
using Domain.Primitives;

namespace Application.Features.Events.Commands.AddTicketType
{
    public class AddTicketTypeCommandHandler : ICommandHandler<AddTicketTypeCommand>
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AddTicketTypeCommandHandler( IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository,
                                           IDateTimeProvider dateTimeProvider)
        {
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
        }
        public async Task<Result> Handle(AddTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var EventIdResult = EventId.Create(request.EventId);
            var @event = await _eventRepository.GetByIdAsync(
                                                EventIdResult.Value,
                                                           cancellationToken);
            if (@event is null)
                return Result.Failure(EventErrors.EventNotFound(EventIdResult.Value));

            var moneyResult = Money.Create(request.Amount,
                                           request.Currency);
            if (moneyResult.IsFailure)
                return Result.Failure(moneyResult.Errors.ToArray());
                
            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);
            var AddTicketresult = @event.AddTicketType(request.Name,
                                                  moneyResult.Value,
                                                  request.Capacity,
                                                  _dateTimeProvider,
                                                  metadata);
            if (AddTicketresult.IsFailure)
                return Result.Failure(AddTicketresult.Errors.ToArray());


            var addResult = await _unitOfWork.CommitAsync();
            
            if (addResult<=0)
            {
                return Result.Failure(
                    Error.Failure(
                    "TicketTypeCreationFailed",
                    "Failed to add ticket type to the event"));
            }

            return Result.Success();

        }
    }
}
