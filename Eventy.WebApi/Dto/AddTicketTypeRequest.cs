namespace Eventy.WebApi.Dto
{
    public record AddTicketTypeRequest(string Name,
                                       decimal Amount,
                                       string Currency,
                                       int Capacity);

}
