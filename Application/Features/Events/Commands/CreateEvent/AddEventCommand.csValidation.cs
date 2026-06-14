using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.CreateEvent
{
    public sealed record AddEventCommand(
        string Name,
        DateTime Date,
        string Country,
        string City,
        string Street,
        int Capacity,
        string Description) : ICommand<Guid>;
}
