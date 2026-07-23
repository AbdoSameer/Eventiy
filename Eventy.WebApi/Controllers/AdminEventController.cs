using Application.Features.Events.Commands.PublishEvent;
using Eventy.WebApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers;

[Route("api/admin/events")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminEventController : ControllerBase
{
    private readonly ISender _sender;

    public AdminEventController(ISender sender) => _sender = sender;

    [HttpPost("{eventId:guid}/publish")]
    public async Task<IActionResult> PublishEvent(Guid eventId, CancellationToken ct)
    {
        var result = await _sender.Send(new PublishEventCommand(eventId), ct);
        return result.ToActionResult();
    }
}
