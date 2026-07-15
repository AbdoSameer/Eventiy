using System.Collections.Concurrent;
using Application.Abstractions.Payments;
using Domain.Common;

namespace Eventy.Testing.Foundation.Fakes;

/// <summary>
/// Test double for <see cref="IPaymentService"/>. Records every call so tests
/// can assert on idempotency keys, cancellation attempts, and retry behaviour.
/// </summary>
public sealed class FakePaymentService : IPaymentService
{
    private volatile bool _shouldFailInitiation;

    /// <summary>
    /// Number of times <see cref="CancelPaymentAsync"/> should fail before
    /// succeeding. Set via <see cref="SetCancelFailCount"/>. Each failed call
    /// returns <c>Result.Failure</c>; once the count is exhausted, subsequent
    /// calls succeed. Used to simulate transient gateway errors (e.g. HTTP 503)
    /// for the compensation-retry scenario.
    /// </summary>
    private int _cancelFailuresRemaining;

    /// <summary>Idempotency keys received by <see cref="InitiatePaymentAsync"/>, keyed by bookingId.</summary>
    public ConcurrentDictionary<Guid, string> IdempotencyKeysReceived { get; } = new();

    /// <summary>Number of times <see cref="CancelPaymentAsync"/> has been invoked per booking.</summary>
    public ConcurrentDictionary<Guid, int> CancelCallCounts { get; } = new();

    /// <summary>Booking IDs whose payment session was successfully cancelled.</summary>
    public HashSet<Guid> CancelledBookings { get; } = new();

    public void SetFailMode(bool shouldFail) => _shouldFailInitiation = shouldFail;

    /// <summary>
    /// Configures the next <paramref name="count"/> cancellation calls to fail,
    /// after which cancellations succeed. Mirrors a gateway returning 503 then
    /// recovering.
    /// </summary>
    public void SetCancelFailCount(int count) => _cancelFailuresRemaining = count;

    public Task<Result<PaymentInitiationResult>> InitiatePaymentAsync(
        Guid bookingId,
        string referenceCode,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        IdempotencyKeysReceived[bookingId] = idempotencyKey;

        if (_shouldFailInitiation)
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
        CancelCallCounts.AddOrUpdate(bookingId, 1, (_, n) => n + 1);

        // Atomically consume a failure slot if any remain.
        if (Interlocked.CompareExchange(ref _cancelFailuresRemaining, 0, 0) > 0)
        {
            Interlocked.Decrement(ref _cancelFailuresRemaining);
            return Task.FromResult(Result.Failure(
                Error.Failure("Payment.CancelFailed", "Simulated transient gateway error (503)")));
        }

        CancelledBookings.Add(bookingId);
        return Task.FromResult(Result.Success());
    }

    /// <summary>Resets all recorded state between tests.</summary>
    public void Reset()
    {
        _shouldFailInitiation = false;
        _cancelFailuresRemaining = 0;
        IdempotencyKeysReceived.Clear();
        CancelCallCounts.Clear();
        CancelledBookings.Clear();
    }
}
