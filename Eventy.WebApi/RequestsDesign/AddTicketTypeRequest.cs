using System.ComponentModel.DataAnnotations;

namespace Eventy.WebApi.RequestsDesign
{
    public record AddTicketTypeRequest(
        [Required] string Name,
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")] decimal Amount,
        [Required, StringLength(3, MinimumLength = 3)] string Currency,
        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1")] int Capacity);

    public record UpdatePhotoMetadataRequest(
        [StringLength(500)] string? Caption,
        [Range(0, int.MaxValue)] int? DisplayOrder);

    public record ReorderPhotosRequest(
        [Required, MinLength(1)] List<Guid> OrderedPhotoIds);

    public record ToggleHighDemandRequest(bool Enabled);
}
