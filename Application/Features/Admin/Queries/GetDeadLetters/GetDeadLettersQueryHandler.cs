using Application.Abstractions.Messaging;
using Application.Abstractions.Outbox;
using Domain.Common;

namespace Application.Features.Admin.Queries.GetDeadLetters;

internal sealed class GetDeadLettersQueryHandler(
    IOutboxRepository outboxRepository) : IQueryHandler<GetDeadLettersQuery, IReadOnlyList<DeadLetterDto>>
{
    public async Task<Result<IReadOnlyList<DeadLetterDto>>> Handle(
        GetDeadLettersQuery request, CancellationToken ct)
    {
        var deadLetters = await outboxRepository.GetDeadLettersAsync(ct);
        return Result<IReadOnlyList<DeadLetterDto>>.Success(deadLetters);
    }
}
