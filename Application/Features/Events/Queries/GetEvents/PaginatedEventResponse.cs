namespace Application.Features.Events.Queries.GetEvents
{
    public sealed record PaginatedEventResponse(
        List<EventCardResponse> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages);
}
