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
            var eventResult = await _eventRepository.GetByIdAsync(
                EventId.CreateUuid(request.EventId),
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
                Location = new AddressResponse
                {
                    Country = eventResult.Value.Location.Country,
                    City = eventResult.Value.Location.City,
                    Street = eventResult.Value.Location.Street
                },
                TicketDetails = eventResult.Value.TicketTypes
                    .Select(ticketType => new TicketDetailsResponse
                    {
                        Id = ticketType.Id.Value,
                        Name = ticketType.Name,
                        Price = ticketType.Price.Amount,
                        Currency = ticketType.Price.Currency,
                        Capacity = ticketType.Capacity
                    })
                    .ToList()
            };

            return Result<EventDetailsResponse>.Success(eventDetails);
        }
    }
}
