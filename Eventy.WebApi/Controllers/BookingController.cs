using Application.Features.Bookings.Command.CancelBooking;
using Application.Features.Bookings.Command.ConfirmBooking;
using Application.Features.Bookings.Command.MakeBooking;
using Application.Features.Bookings.Query.GetBookingByEvent;
using Application.Features.Bookings.Query.GetBookingDetails;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly ISender _sender;

        public BookingController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingDetails(Guid id, CancellationToken cancellationToken)
        {
            var bookings = await _sender.Send(new GetBookingDetailsQuery(id), cancellationToken);
            if (bookings.IsFailure)
                return NotFound(bookings.Error);

            return Ok(bookings.Value);
        }

        [HttpGet("bookings/event/{eventId}")]
        public async Task<IActionResult> GetBookingsByEventId(Guid eventId, CancellationToken cancellationToken)
        {
            var bookings = await _sender.Send(new GetBookingByEventQuery(eventId), cancellationToken);
            if (bookings.IsFailure)
                return NotFound(bookings.Error);

            return Ok(bookings.Value);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] MakeBookingCommand command, CancellationToken cancellationToken)
        {
            var booking = await _sender.Send(command, cancellationToken);
            if (booking.IsFailure)
                return BadRequest(booking.Error);

            return Ok(booking.Value);
        }
        [HttpPost("booking/{bookingId}/confirm")]
        public async Task<IActionResult> ConfirmBooking(Guid bookingId, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new ConfirmBookingCommand(bookingId), cancellationToken);
            if (result.IsFailure)
                return BadRequest(result.Error);
            return Ok(result.Value);
        }

        [HttpPost("booking/{bookingId}cancel")]
        public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(new CancelBookingCommand(bookingId), cancellationToken);
            if (result.IsFailure)
                return BadRequest(result.Error);
            return Ok(result.Value);
        }
    }
}
