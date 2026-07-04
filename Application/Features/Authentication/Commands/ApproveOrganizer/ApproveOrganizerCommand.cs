using Application.Abstractions.Messaging;

namespace Application.Features.Authentication.Commands.ApproveOrganizer;

public sealed record ApproveOrganizerCommand(Guid UserId) : ICommand;
