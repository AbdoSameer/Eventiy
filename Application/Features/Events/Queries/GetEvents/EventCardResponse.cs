namespace Application.Features.Events.Queries.GetEvents
{
    public record EventCardResponse(
              Guid Id,
              string Title,
              DateTime Date);

}