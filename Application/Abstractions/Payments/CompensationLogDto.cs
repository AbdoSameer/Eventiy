namespace Application.Abstractions.Payments;

public sealed record CompensationLogDto(
    Guid Id,
    Guid BookingId,
    string CompensationType,
    string Payload,
    DateTime OccurredOnUtc,
    string IdempotencyKey,
    DateTime? ProcessedOnUtc,
    string? Error,
    int RetryCount,
    DateTime? NextRetryOnUtc);
