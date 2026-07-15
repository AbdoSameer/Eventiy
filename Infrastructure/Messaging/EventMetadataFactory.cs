using Application.Abstractions;
using Application.Abstractions.Security;
using Domain.Common;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Messaging;

internal sealed class EventMetadataFactory : IEventMetadataFactory
{
    private const string CorrelationIdKey = "CorrelationId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUserService;

    public EventMetadataFactory(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserService = currentUserService;
    }

    public EventMetadata Create()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var correlationId = httpContext?.Items.TryGetValue(CorrelationIdKey, out var id) == true
            ? id?.ToString()
            : Guid.NewGuid().ToString();

        var causationId = Guid.NewGuid().ToString();

        var userId = _currentUserService.IsAuthenticated
            ? _currentUserService.GetCurrentUserId().Value?.Value.ToString()
            : null;

        return new EventMetadata(correlationId ?? Guid.NewGuid().ToString(), causationId, userId);
    }
}
