namespace Application.Features.Events.Queries.GetEventPhotos;

public sealed record EventPhotoResponse(
    Guid Id,
    string PublicUrl,
    string? Caption,
    int DisplayOrder,
    bool IsCover,
    DateTime UploadedAt);
