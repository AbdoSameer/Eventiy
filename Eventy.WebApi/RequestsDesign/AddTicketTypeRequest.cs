namespace Eventy.WebApi.RequestsDesign
{
    public record AddTicketTypeRequest(string Name,
                                       decimal Amount,
                                       string Currency,
                                       int Capacity);

}