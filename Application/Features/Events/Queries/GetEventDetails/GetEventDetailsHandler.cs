using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public sealed class GetEventDetailsHandler :
        IQueryHandler<GetEventDetailsQuery, EventDetailsResponse>
    {
        private readonly IEventRepository _eventRepository;

        public GetEventDetailsHandler(IEventRepository eventRepository)
        {
            _eventRepository = eventRepository;
        }

        public async Task<Result<EventDetailsResponse>> Handle(
            GetEventDetailsQuery request,
            CancellationToken cancellationToken)
        {
            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
            {
                return Result<EventDetailsResponse>.Failure(eventIdResult.Error);
            }

            var eventResult = await _eventRepository.GetByIdAsync(
                eventIdResult.Value,
                cancellationToken);

            if (eventResult.IsFailure)
            {
                return Result<EventDetailsResponse>.Failure(eventResult.Error);
            }

            if (eventResult.Value is null)
            {
                return Result<EventDetailsResponse>.Failure($"Event with ID {request.EventId} was not found.");
            }

            var eventDetails = new EventDetailsResponse
            {
                Id = eventResult.Value.Id.Value,
                Date = eventResult.Value.Date,
                Name = eventResult.Value.Name.Value,
                Description = eventResult.Value.Description,
                Status = eventResult.Value.Status,
                LowestTicketPrice = eventResult.Value.TicketTypes.Count == 0
                    ? 0
                    : eventResult.Value.TicketTypes.Min(ticketType => ticketType.Price.Amount),
                Location = new AddressResponse(
                    eventResult.Value.Location.Country,
                    eventResult.Value.Location.City,
                    eventResult.Value.Location.Street),
                TicketDetails = eventResult.Value.TicketTypes
                    .Select(ticketType => new TicketDetailsResponse(
                        ticketType.Id.Value,
                        ticketType.Price.Amount,
                        ticketType.Price.Currency,
                        ticketType.Name,
                        ticketType.Capacity))
                    .ToList()
            };

            return Result<EventDetailsResponse>.Success(eventDetails);
        }
    }
}
