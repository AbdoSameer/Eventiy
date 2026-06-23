using Application.Features.Events.Commands.AddTicketType;
using Application.Features.Events.Commands.CreateEvent;
using Application.Features.Events.Queries.GetEventDetails;
using Application.Features.Events.Queries.GetEvents;
using Azure.Core;
using Eventy.WebApi.ControllerErorrs;
using Eventy.WebApi.RequestsDesign;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly ISender _sender;

        public EventController(ISender sender)
        {
            _sender = sender;
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await _sender
                .Send(new GetEventDetailsQuery(id), cancellationToken);
            if (events.IsFailure)
                return NotFound(events.Errors);

            return Ok(events.Value);
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(CancellationToken cancellationToken)
        {

            var events = await _sender.Send(new GetEventsQuery(), cancellationToken);

            if (events.IsFailure)
                return NotFound(events.Errors);

            return Ok(events.Value);
        }
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventCommand command,
            CancellationToken cancellationToken)
        {

            var events = await _sender.Send(command, cancellationToken);

            if (events.IsFailure)
                return BadRequest(events.Errors);

            return Created($"api/events/{events.Value}", events.Value);
        }

        [HttpPost]
        [Route("{eventId}/ticket-types")]
        public async Task<IActionResult> CreateTicketType(Guid eventId,
            [FromBody] AddTicketTypeRequest request,
            CancellationToken cancellationToken)
        {
            var command = new AddTicketTypeCommand(
                eventId,
                request.Name,
                request.Amount,
                request.Currency,
                request.Capacity
            );
            
            var ticketType = await _sender.Send(command, cancellationToken);

            if (ticketType.IsFailure)
                return BadRequest(ticketType.Errors);

            return Created($"api/events/{eventId}/ticket-types", null);
        }
    }
}
