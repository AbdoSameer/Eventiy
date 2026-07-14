using Domain.Common;

namespace Application.Abstractions.Payments
{
    public sealed record PaymentInitiationResult(
        string PaymentUrl,
        string? ClientSecret);

    public interface IPaymentService
    {
        Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
            Guid bookingId,
            string referenceCode,
            decimal amount,
            string currency,
            CancellationToken ct = default);

        Task<Result> CancelPaymentAsync(
            Guid bookingId,
            CancellationToken ct = default);
    }
}
