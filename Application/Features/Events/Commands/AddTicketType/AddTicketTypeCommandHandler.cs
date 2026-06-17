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
        private readonly IAddTicketTypeRepository _addTicketType;
        private readonly IUnitOfWork _unitOfWork;

        public AddTicketTypeCommandHandler(IAddTicketTypeRepository addTicketType,
                                           IUnitOfWork unitOfWork)
        {
            _addTicketType = addTicketType;
            _unitOfWork = unitOfWork;
        }
        public async Task<Result> Handle(AddTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var @result = TicketType
                .Create(
                    new EventId(request.EventId),
                    request.Name,
                    Money.Create(request.Amount, request.Currency).Value,
                    request.capacity
                );
            if (@result.IsFailure)
            {
                return Result.Failure(result.Error); 
            }

            await _addTicketType.AddTicketTypeAsync(@result.Value, cancellationToken);

            var addResult = await _unitOfWork.CommitAsync(cancellationToken);
            
            if (addResult.IsFailure)
            {
                return Result.Failure(addResult.Error);
            }

            return Result.Success();

        }
    }
}
