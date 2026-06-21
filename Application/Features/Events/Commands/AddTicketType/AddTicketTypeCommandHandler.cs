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
            var GetEventResult = EventId.Create(request.EventId);
            var @event = await _eventRepository.GetByIdAsync(
                                                GetEventResult.Value,
                                                           cancellationToken);
            if (@event is null)
                return Result.Failure("Event not found");

            var moneyResult = Money.Create(request.Amount,
                                           request.Currency);
            if (moneyResult.IsFailure)
                return Result.Failure(moneyResult.Error);
                
            var AddTicketresult = @event.AddTicketType(request.Name,
                                                  moneyResult.Value,
                                                  request.Capacity);
            if (AddTicketresult.IsFailure)
                return Result.Failure(AddTicketresult.Error);


            var addResult = await _unitOfWork.CommitAsync();
            
            if (addResult<=0)
            {
                return Result.Failure("Failed to add ticket type.");
            }

            return Result.Success();

        }
    }
}
