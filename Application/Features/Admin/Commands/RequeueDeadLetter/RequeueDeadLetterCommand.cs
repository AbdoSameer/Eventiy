using Application.Abstractions.Messaging;

namespace Application.Features.Admin.Commands.RequeueDeadLetter;

public sealed record RequeueDeadLetterCommand(Guid Id) : ICommand;
