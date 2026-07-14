using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Payments;

public sealed class StripeSettings
{
    public const string SectionName = "Stripe";

    [Required] public string SecretKey { get; init; } = string.Empty;
    [Required] public string WebhookSecret { get; init; } = string.Empty;
    [Required] public string SuccessUrl { get; init; } = string.Empty;
    [Required] public string CancelUrl { get; init; } = string.Empty;
}
