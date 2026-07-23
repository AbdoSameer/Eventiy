using Application.Abstractions.Messaging;
using Application.Abstractions.Outbox;

namespace Application.Features.Admin.Queries.GetDeadLetters;

public sealed record GetDeadLettersQuery : IQuery<IReadOnlyList<DeadLetterDto>>;
