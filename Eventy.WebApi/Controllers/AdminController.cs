using Application.Features.Admin.Commands.RequeueDeadLetter;
using Application.Features.Admin.Queries.GetDeadLetters;
using Eventy.WebApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers;

[Route("api/admin/outbox")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ISender _sender;

    public AdminController(ISender sender) => _sender = sender;

    [HttpGet("dead-letters")]
    public async Task<IActionResult> GetDeadLetters(CancellationToken ct)
    {
        var result = await _sender.Send(new GetDeadLettersQuery(), ct);
        return result.ToActionResult();
    }

    [HttpPost("dead-letters/{id:guid}/requeue")]
    public async Task<IActionResult> RequeueDeadLetter(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new RequeueDeadLetterCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
