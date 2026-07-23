using Application.Features.Bookings.Query.GetAllBookings;
using Eventy.WebApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers;

[Route("api/admin/bookings")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminBookingController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBookingController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAllBookings(
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetAllBookingsQuery(status), ct);
        return result.ToActionResult();
    }
}
