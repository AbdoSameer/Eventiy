using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.ToggleHighDemand;

public sealed record ToggleHighDemandCommand : ICommand<ToggleHighDemandResponse>
{
    public Guid EventId { get; init; }
    public bool Enabled { get; init; }
}

public sealed record ToggleHighDemandResponse(bool IsHighDemand);
