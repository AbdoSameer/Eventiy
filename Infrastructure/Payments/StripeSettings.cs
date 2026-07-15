using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Payments;

public sealed class StripeSettings
{
    public const string SectionName = "Stripe";

    public bool UseMock { get; init; }
    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public string SuccessUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
}
