namespace Eventy.WebApi.RequestsDesign
{
    public record AddTicketTypeRequest(string Name,
                                       decimal Amount,
                                       string Currency,
                                       int Capacity);

    public record UpdatePhotoMetadataRequest(string? Caption, int? DisplayOrder);

    public record ReorderPhotosRequest(List<Guid> OrderedPhotoIds);

    public record ToggleHighDemandRequest(bool Enabled);
}