using Application.Features.Events.Commands.AddTicketType;
using Application.Features.Events.Commands.CreateEvent;
using Application.Features.Events.Queries.GetEventDetails;
using Application.Features.Events.Queries.GetEvents;
using Eventy.WebApi.Extensions; 
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
        public EventController(ISender sender) => _sender = sender;

        [HttpGet("{id}", Name = nameof(GetEvent))]
        public async Task<IActionResult> GetEvent(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetEventDetailsQuery(id), ct);
            return result.ToActionResult();
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(CancellationToken ct)
        {
            var result = await _sender.Send(new GetEventsQuery(), ct);
            return result.ToActionResult();
        }

        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);

            return result.IsSuccess
                ? CreatedAtRoute(nameof(GetEvent), new { id = result.Value }, result.Value)
                : result.ToActionResult();
        }

        [HttpPost("{eventId}/ticket-types")]
        public async Task<IActionResult> CreateTicketType(
            Guid eventId,
            [FromBody] AddTicketTypeRequest request,
            CancellationToken ct)
        {
            var command = new AddTicketTypeCommand(
                eventId, request.Name, request.Amount, request.Currency, request.Capacity);

            var result = await _sender.Send(command, ct);

            return result.IsSuccess
                ? CreatedAtRoute(nameof(GetEvent), new { eventId }, null)
                : result.ToActionResult();
        }
    }
}