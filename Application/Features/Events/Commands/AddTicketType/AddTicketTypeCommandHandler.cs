using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

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
            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result.Failure(eventIdResult.Error);

            var eventResult = await _eventRepository.GetByIdAsync(
                                                eventIdResult.Value,
                                                           cancellationToken);

            if (eventResult.IsFailure)
                return Result.Failure(eventResult.Error);

            if (eventResult.Value is null)
                return Result.Failure("Event not found.");

            var moneyResult = Money.Create(request.Amount,
                                           request.Currency);
            if (moneyResult.IsFailure)
                return Result.Failure(moneyResult.Error);
                
            var AddTicketresult = eventResult.Value.AddTicketType(request.Name,
                                                  moneyResult.Value,
                                                  request.Capacity);
            if (AddTicketresult.IsFailure)
                return Result.Failure(AddTicketresult.Error);


            var addResult = await _unitOfWork.CommitAsync(cancellationToken);
            
            if (addResult.IsFailure)
            {
                return Result.Failure(addResult.Error);
            }

            return Result.Success();

        }
    }
}
