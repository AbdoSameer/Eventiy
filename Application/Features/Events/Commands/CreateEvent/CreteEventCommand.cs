using Application.Abstractions.Messaging;
using Domain.Primitives;

namespace Application.Features.Events.Commands.CreateEvent
{
    public sealed record CreateEventCommand
       (
        string Name,
        int Capacity,
        DateTime Date,
        Address Location,
        string Description) : ICommand<Guid>;
    
}
