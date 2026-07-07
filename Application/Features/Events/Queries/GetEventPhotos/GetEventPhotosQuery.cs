using Application.Abstractions.Messaging;

namespace Application.Features.Events.Queries.GetEventPhotos;

public sealed record GetEventPhotosQuery(Guid EventId) : IQuery<List<EventPhotoResponse>>;
