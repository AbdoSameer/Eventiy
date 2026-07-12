using Application.Features.Bookings.Command.CancelBooking;
using Application.Features.Bookings.Command.ConfirmBooking;
using Application.Features.Bookings.Command.ConfirmDeferredPayment;
using Application.Features.Bookings.Command.CreateBooking;
using Application.Features.Bookings.Query.GetBookingsByUser;
using Application.Features.Bookings.Query.GetBookingByEvent;
using Application.Features.Bookings.Query.GetBookingDetails;
using Eventy.WebApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly ISender _sender;
        public BookingController(ISender sender) => _sender = sender;

        [HttpGet("{id}", Name = nameof(GetBookingDetails))]
        public async Task<IActionResult> GetBookingDetails(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetBookingDetailsQuery(id), ct);
            return result.ToActionResult();
        }

        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetBookingsByEventId(Guid eventId, CancellationToken ct)
        {
            var result = await _sender.Send(new GetBookingByEventQuery(eventId), ct);
            return result.ToActionResult();
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking(
            [FromBody] CreateBookingCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);

            return result.IsSuccess
                ? CreatedAtRoute(nameof(GetBookingDetails), new { id = result.Value.BookingId }, result.Value)
                : result.ToActionResult();
        }

        [HttpPost("{bookingId:guid}/confirm")]
        public async Task<IActionResult> ConfirmBooking(Guid bookingId, CancellationToken ct)
        {
            var result = await _sender.Send(new ConfirmBookingCommand(bookingId), ct);
            return result.ToActionResult();
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> CancelBooking(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new CancelBookingCommand(id), ct);
            return result.ToActionResult();
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyBookings(CancellationToken ct)
        {
            var result = await _sender.Send(new GetBookingsByUserQuery(), ct);
            return result.ToActionResult();
        }

        [HttpPost("confirm-deferred")]
        public async Task<IActionResult> ConfirmDeferredPayment(
            [FromBody] ConfirmDeferredPaymentCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            return result.ToActionResult();
        }
    }
}