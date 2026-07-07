using Application.Abstractions.Messaging;
using Application.Features.Events.Queries.GetEventDetails;

namespace Application.Features.Events.Commands.UpdateEvent;

public sealed record UpdateEventCommand(
    Guid EventId,
    string Name,
    int Capacity,
    DateTime Date,
    AddressResponse Location,
    string Description) : ICommand;
