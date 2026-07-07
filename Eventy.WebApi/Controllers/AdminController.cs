using Application.Abstractions.Outbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers;

[Route("api/admin/outbox")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IOutboxRepository _outboxRepository;

    public AdminController(IOutboxRepository outboxRepository) =>
        _outboxRepository = outboxRepository;

    [HttpGet("dead-letters")]
    public async Task<IActionResult> GetDeadLetters(CancellationToken ct)
    {
        var deadLetters = await _outboxRepository.GetDeadLettersAsync(ct);
        return Ok(deadLetters);
    }

    [HttpPost("dead-letters/{id:guid}/requeue")]
    public async Task<IActionResult> RequeueDeadLetter(Guid id, CancellationToken ct)
    {
        await _outboxRepository.RequeueDeadLetterAsync(id, ct);
        return NoContent();
    }
}
