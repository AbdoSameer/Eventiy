using Application.Abstractions;
using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Events.Commands.AddTicketType
{
    public class AddTicketTypeCommandHandler : ICommandHandler<AddTicketTypeCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEventRepository _eventRepository;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICacheService _cache;
        private readonly IVenueLayoutValidator _venueLayout;
        private readonly ICurrentUserService _currentUser;

        public AddTicketTypeCommandHandler(
            IUnitOfWork unitOfWork,
            IEventRepository eventRepository,
            TimeProvider dateTimeProvider,
            ICacheService cache,
            IVenueLayoutValidator venueLayout,
            ICurrentUserService currentUser)
        {
            _unitOfWork = unitOfWork;
            _eventRepository = eventRepository;
            _dateTimeProvider = dateTimeProvider;
            _cache = cache;
            _venueLayout = venueLayout;
            _currentUser = currentUser;
        }

        public async Task<Result> Handle(AddTicketTypeCommand request, CancellationToken cancellationToken)
        {
            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result.Failure(eventIdResult.Errors.ToArray());

            var @event = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
            if (@event is null)
                return Result.Failure(EventErrors.EventNotFound(eventIdResult.Value));

            var sectionValidation = _venueLayout.ValidateSectionCode(@event.Type, request.SectionCode);
            if (sectionValidation.IsFailure)
                return sectionValidation;

            var moneyResult = Money.Create(request.Amount, request.Currency);
            if (moneyResult.IsFailure)
                return Result.Failure(moneyResult.Errors.ToArray());

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var isAdmin = _currentUser.GetCurrentUserRole() == "Admin";
            Result addTicketResult;
            var venueType = @event.Type.ToString();
            if (isAdmin)
            {
                addTicketResult = @event.AdminAddTicketType(request.Name,
                    moneyResult.Value,
                    request.Capacity,
                    utcNow,
                    request.SectionCode,
                    venueType);
            }
            else
            {
                addTicketResult = @event.AddTicketType(request.Name,
                    moneyResult.Value,
                    request.Capacity,
                    utcNow,
                    request.SectionCode,
                    venueType);
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

            await _cache.RemoveAsync(EventDetails(request.EventId), cancellationToken);
            await _cache.RemoveByPatternAsync(EventsListPattern, cancellationToken);

            return Result.Success();
        }
    }
}
