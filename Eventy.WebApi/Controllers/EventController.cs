using Application.Features.Events.Commands.CreateEvent;
using Application.Features.Events.Commands.CancelEvent;
using Application.Features.Events.Commands.ToggleHighDemand;
using Application.Features.Events.Commands.UpdateEvent;
using Application.Features.Events.Commands.AddTicketType;
using Application.Features.Events.Commands.DeleteEventPhoto;
using Application.Features.Events.Commands.ReorderEventPhotos;
using Application.Features.Events.Commands.SetCoverPhoto;
using Application.Features.Events.Commands.UpdatePhotoMetadata;
using Application.Features.Events.Commands.UploadEventPhotos;
using Application.Features.Events.Queries.GetEventDetails;
using Application.Features.Events.Queries.GetEventPhotos;
using Application.Features.Events.Queries.GetEvents;
using System.IO;
using Eventy.WebApi.Extensions; 
using Eventy.WebApi.RequestsDesign;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventy.WebApi.Controllers
{
    [Route("api/events")]
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
        public async Task<IActionResult> GetEvents(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? type = null,
            [FromQuery] double? userLatitude = null,
            [FromQuery] double? userLongitude = null,
            [FromQuery] double distanceInKm = 20,
            CancellationToken ct = default)
        {
            Domain.Aggregates.EventAggregate.Enums.EventType? eventType = null;
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<Domain.Aggregates.EventAggregate.Enums.EventType>(type, ignoreCase: true, out var parsed))
                eventType = parsed;

            var query = new GetEventsQuery(
                page, pageSize, eventType, userLatitude, userLongitude, distanceInKm);
            var result = await _sender.Send(query, ct);
            return result.ToActionResult();
        }

        [Authorize(Roles = "Organizer,Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateEvent(
            [FromBody] CreateEventCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);

            return result.ToCreatedResult(nameof(GetEvent), new { id = result.Value });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> UpdateEvent(
            Guid id, [FromBody] UpdateEventCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command with { EventId = id }, ct);
            return result.ToActionResult();
        }

        [HttpPost("{eventId:guid}/ticket-types")]
        [Authorize(Roles = "Organizer,Admin")]
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

        [HttpPut("{id:guid}/cancel")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> CancelEvent(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new CancelEventCommand(id), ct);
            return result.ToActionResult();
        }

        [HttpPut("{id:guid}/high-demand")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> ToggleHighDemand(
            Guid id, [FromBody] ToggleHighDemandRequest request, CancellationToken ct)
        {
            var result = await _sender.Send(
                new ToggleHighDemandCommand { EventId = id, Enabled = request.Enabled }, ct);
            return result.ToActionResult();
        }

        // ===== Photo Endpoints ==============================================

        [HttpGet("{id}/photos")]
        [AllowAnonymous]
        public async Task<IActionResult> GetEventPhotos(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetEventPhotosQuery(id), ct);
            return result.ToActionResult();
        }

        [HttpPost("{id}/photos")]
        [Authorize(Roles = "Organizer,Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadEventPhotos(
            Guid id,
            [FromForm] List<IFormFile> photos,
            CancellationToken ct)
        {
            if (photos == null || photos.Count == 0)
                return BadRequest(new ProblemDetails
                {
                    Title = "Validation Error",
                    Detail = "No photo files provided.",
                    Status = StatusCodes.Status400BadRequest,
                    Extensions =
                    {
                        ["errors"] = new[] { new { code = "NoFiles", message = "No photo files provided." } }
                    }
                });

            var fileData = new List<FileUploadData>();
            foreach (var f in photos)
            {
                using var stream = f.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                fileData.Add(new FileUploadData(
                    ms.ToArray(),
                    f.FileName,
                    f.ContentType,
                    f.Length));
            }

            var command = new UploadEventPhotosCommand(id, fileData);
            var result = await _sender.Send(command, ct);
            return result.ToActionResult();
        }

        [HttpDelete("{id}/photos/{photoId}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> DeleteEventPhoto(Guid id, Guid photoId, CancellationToken ct)
        {
            var result = await _sender.Send(new DeleteEventPhotoCommand(id, photoId), ct);
            return result.IsSuccess ? NoContent() : result.ToActionResult();
        }

        [HttpPut("{id}/photos/{photoId}/cover")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> SetCoverPhoto(Guid id, Guid photoId, CancellationToken ct)
        {
            var result = await _sender.Send(new SetCoverPhotoCommand(id, photoId), ct);
            return result.IsSuccess ? NoContent() : result.ToActionResult();
        }

        [HttpPut("{id}/photos/{photoId}/metadata")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> UpdatePhotoMetadata(
            Guid id, Guid photoId,
            [FromBody] UpdatePhotoMetadataRequest request,
            CancellationToken ct)
        {
            var command = new UpdatePhotoMetadataCommand(id, photoId, request.Caption, request.DisplayOrder);
            var result = await _sender.Send(command, ct);
            return result.IsSuccess ? NoContent() : result.ToActionResult();
        }

        [HttpPut("{id}/photos/reorder")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> ReorderEventPhotos(
            Guid id,
            [FromBody] ReorderPhotosRequest request,
            CancellationToken ct)
        {
            var command = new ReorderEventPhotosCommand(id, request.OrderedPhotoIds);
            var result = await _sender.Send(command, ct);
            return result.IsSuccess ? NoContent() : result.ToActionResult();
        }
    }
}