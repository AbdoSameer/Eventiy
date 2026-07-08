using Application.Features.Events.Commands.AddTicketType;
using Application.Features.Events.Commands.PublishEvent;
using Application.Features.Events.Commands.UpdateEvent;
using Eventy.WebApi.Extensions;
using Eventy.WebApi.RequestsDesign;
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateEvent(
        Guid id, [FromBody] UpdateEventCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command with { EventId = id }, ct);
        return result.ToActionResult();
    }

    [HttpPost("{eventId:guid}/ticket-types")]
    public async Task<IActionResult> AddTicketType(
        Guid eventId,
        [FromBody] AddTicketTypeRequest request,
        CancellationToken ct)
    {
        var command = new AddTicketTypeCommand(
            eventId, request.Name, request.Amount, request.Currency, request.Capacity);

        var result = await _sender.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(UpdateEvent), new { id = eventId }, null)
            : result.ToActionResult();
    }

    [HttpPost("{eventId:guid}/publish")]
    public async Task<IActionResult> PublishEvent(Guid eventId, CancellationToken ct)
    {
        var result = await _sender.Send(new PublishEventCommand(eventId), ct);
        return result.ToActionResult();
    }
}
