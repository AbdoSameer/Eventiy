using Application.Abstractions.Messaging;
using Application.Abstractions.Outbox;
using Domain.Common;

namespace Application.Features.Admin.Commands.RequeueDeadLetter;

internal sealed class RequeueDeadLetterCommandHandler(
    IOutboxRepository outboxRepository) : ICommandHandler<RequeueDeadLetterCommand>
{
    public async Task<Result> Handle(RequeueDeadLetterCommand request, CancellationToken ct)
    {
        await outboxRepository.RequeueDeadLetterAsync(request.Id, ct);
        return Result.Success();
    }
}
