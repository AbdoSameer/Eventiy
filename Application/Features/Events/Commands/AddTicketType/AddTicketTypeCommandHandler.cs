using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Events.Commands.AddTicketType
{
    public class AddTicketTypeCommandHandler : ICommandHandler<AddTicketTypeCommand>
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;

        public AddTicketTypeCommandHandler( IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository)
        {
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
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
                
            var AddTicketresult = @event.AddTicketType(request.Name,
                                                  moneyResult.Value,
                                                  request.Capacity);
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
