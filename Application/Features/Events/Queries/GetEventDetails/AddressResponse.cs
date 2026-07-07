namespace Application.Features.Events.Queries.GetEventDetails
{
    public record AddressResponse(
        string Country,
        string City,
        string Street,
        double? Latitude = null,
        double? Longitude = null);

}
