using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;

namespace Application.Features.Events.Commands.AddTicketType
{
    public class AddTicketTypeCommandHandler : ICommandHandler<AddTicketTypeCommand>
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly TimeProvider _dateTimeProvider;
        private readonly IEventMetadataFactory _metadataFactory;
        private readonly ICurrentUserService _currentUser;
        private readonly ICacheService _cache;

        public AddTicketTypeCommandHandler( IUnitOfWork unitOfWork ,
                                           IEventRepository eventRepository,
                                           TimeProvider dateTimeProvider,
                                           IEventMetadataFactory metadataFactory,
                                           ICurrentUserService currentUser,
                                           ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
            _metadataFactory = metadataFactory;
            _currentUser = currentUser;
            _cache = cache;
        }
        public async Task<Result> Handle(AddTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var role = _currentUser.GetCurrentUserRole();
            if (role != "Admin" && role != "Organizer")
                throw new UnauthorizedAccessException("Only administrators or organizers can add ticket types.");

            var isAdmin = role == "Admin";

            var EventIdResult = EventId.Create(request.EventId);
            if (EventIdResult.IsFailure)
                return Result.Failure(EventIdResult.Errors.ToArray());

            var @event = await _eventRepository.GetByIdAsync(
                                                EventIdResult.Value,
                                                           cancellationToken);
            if (@event is null)
                return Result.Failure(EventErrors.EventNotFound(EventIdResult.Value));

            var moneyResult = Money.Create(request.Amount,
                                           request.Currency);
            if (moneyResult.IsFailure)
                return Result.Failure(moneyResult.Errors.ToArray());
                
            var metadata = _metadataFactory.Create();
            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            Result addTicketResult;
            if (isAdmin)
            {
                addTicketResult = @event.AdminAddTicketType(request.Name,
                    moneyResult.Value,
                    request.Capacity,
                    utcNow,
                    metadata);
            }
            else
            {
                addTicketResult = @event.AddTicketType(request.Name,
                    moneyResult.Value,
                    request.Capacity,
                    utcNow,
                    metadata);
            }

            if (addTicketResult.IsFailure)
                return Result.Failure(addTicketResult.Errors.ToArray());

            var addResult = await _unitOfWork.CommitAsync();
            
            if (addResult <= 0)
            {
                return Result.Failure(
                    Error.Failure(
                    "TicketTypeCreationFailed",
                    "Failed to add ticket type to the event"));
            }

            await _cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);
            await _cache.RemoveByPatternAsync("events:list:*", cancellationToken);

            return Result.Success();

        }
    }
}
