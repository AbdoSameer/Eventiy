using Application.Abstractions.Messaging;
using Application.Features.Events.Queries.GetEventPhotos;

namespace Application.Features.Events.Commands.UploadEventPhotos;

public sealed record FileUploadData(
    byte[] Content,
    string FileName,
    string ContentType,
    long Length);

public sealed record UploadEventPhotosCommand(
    Guid EventId,
    List<FileUploadData> Photos) : ICommand<List<EventPhotoResponse>>;
