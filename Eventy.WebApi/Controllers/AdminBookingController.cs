using Application.Features.Bookings.Command.CancelBooking;
using Application.Features.Bookings.Command.ConfirmBooking;
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

    [HttpPost("{bookingId:guid}/confirm")]
    public async Task<IActionResult> ConfirmBooking(Guid bookingId, CancellationToken ct)
    {
        var result = await _sender.Send(new ConfirmBookingCommand(bookingId), ct);
        return result.ToActionResult();
    }

    [HttpPut("{bookingId:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelBookingCommand(bookingId), ct);
        return result.ToActionResult();
    }
}
