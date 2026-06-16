using Application.Abstractions.Messaging;
using Application.Features.Events.Queries.GetEventDetails;
using Domain.Primitives;

namespace Application.Features.Events.Commands.CreateEvent
{
    public sealed record CreateEventCommand
       (
        string Name,
        int Capacity,
        DateTime Date,
        AddressResponse Location,
        string Description) : ICommand<Guid>;
}
