using Application.Abstractions.Payments;
using Domain.Common;

namespace Eventy.Testing.Foundation.Fakes;

public sealed class FakePaymentService : IPaymentService
{
    private volatile bool _shouldFail;

    public void SetFailMode(bool shouldFail) => _shouldFail = shouldFail;

    public Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
        Guid bookingId, string referenceCode, decimal amount, string currency,
        CancellationToken ct = default)
    {
        if (_shouldFail)
        {
            return Task.FromResult(Result<PaymentInitiationResult>.Failure(
                Error.Failure("Payment.InitiationFailed", "Simulated payment failure")));
        }

        return Task.FromResult(Result<PaymentInitiationResult>.Success(
            new PaymentInitiationResult("https://fake-payment.example.com/success", ClientSecret: null)));
    }

    public Task<Result> CancelPaymentAsync(
        Guid bookingId,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result.Success());
    }
}
