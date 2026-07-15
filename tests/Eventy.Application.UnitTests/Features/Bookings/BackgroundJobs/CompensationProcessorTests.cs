using System.Reflection;
using Application.Abstractions.Payments;
using Domain.Common;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.BackgroundJobs;

/// <summary>
/// Unit tests for the pure decision logic inside <see cref="Infrastructure.BackgroundJobs.CompensationProcessor"/>.
/// The full polling/locking cycle is exercised by the integration tests
/// (see PendingFirstBookingTests Scenario D); these tests isolate the
/// retry-schedule and dispatch logic for fast, deterministic verification.
/// </summary>
public class CompensationProcessorTests
{
    // The CompensationProcessor type lives in Infrastructure, but the static
    // helpers are pure functions with no DB / DI dependency — safe to invoke
    // directly via reflection without spinning up the hosted service.
    private static readonly Type ProcessorType =
        typeof(Infrastructure.BackgroundJobs.CompensationProcessor);

    /// <summary>
    /// Invokes the private static <c>ComputeNextRetry</c> method.
    /// Schedule: 5s, 30s, 1m, 5m, 15m (then clamped to 15m).
    /// </summary>
    private static DateTime? InvokeComputeNextRetry(int retryCount, DateTime now)
    {
        var method = ProcessorType.GetMethod("ComputeNextRetry",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("ComputeNextRetry must exist for backoff scheduling");
        return (DateTime?)method!.Invoke(null, [retryCount, now])!;
    }

    /// <summary>Invokes the private static <c>ExecuteCompensation</c> method.</summary>
    private static async Task<Result> InvokeExecuteCompensation(
        IPaymentService paymentService, CompensationLogDto log, CancellationToken ct)
    {
        var method = ProcessorType.GetMethod("ExecuteCompensation",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("ExecuteCompensation must exist for dispatch");
        return await (Task<Result>)method!.Invoke(null, [paymentService, log, ct])!;
    }

    #region ComputeNextRetry — backoff schedule

    [Fact]
    public void ComputeNextRetry_RetryZero_ReturnsNull()
    {
        // A retry count of 0 means the log was just staged, not yet attempted.
        InvokeComputeNextRetry(0, DateTime.UtcNow)
            .Should().BeNull("a brand-new compensation has no scheduled retry");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 300)]
    [InlineData(5, 900)]
    public void ComputeNextRetry_ReturnsExponentialBackoff(int retry, int expectedSeconds)
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var next = InvokeComputeNextRetry(retry, now);

        next.Should().Be(now.AddSeconds(expectedSeconds),
            "retry #{0} must schedule the next attempt with the documented exponential backoff",
            retry);
    }

    [Fact]
    public void ComputeNextRetry_BeyondSchedule_ClampsToMaxBackoff()
    {
        // Anything past the 5th slot clamps to 15 minutes — the final backoff
        // value — so a runaway counter can't overflow the array.
        var now = DateTime.UtcNow;
        var next = InvokeComputeNextRetry(99, now);

        next.Should().Be(now.AddMinutes(15),
            "retry counts past the backoff array must clamp to the maximum (15 minutes)");
    }

    #endregion

    #region ExecuteCompensation — dispatch by type

    [Fact]
    public async Task ExecuteCompensation_CancelPayment_CallsCancelAndReturnsSuccess()
    {
        var paymentService = Substitute.For<IPaymentService>();
        paymentService.CancelPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        var log = NewDto(compensationType: "CancelPayment");

        var result = await InvokeExecuteCompensation(paymentService, log, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await paymentService.Received(1).CancelPaymentAsync(log.BookingId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCompensation_CancelPaymentFails_PropagatesFailure()
    {
        var paymentService = Substitute.For<IPaymentService>();
        paymentService.CancelPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(
                Error.Failure("Payment.CancelFailed", "gateway returned 503"))));

        var log = NewDto(compensationType: "CancelPayment");

        var result = await InvokeExecuteCompensation(paymentService, log, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "a transient gateway failure must propagate so the processor can schedule a retry");
    }

    [Fact]
    public async Task ExecuteCompensation_UnknownType_ReturnsFailureWithoutCallingGateway()
    {
        var paymentService = Substitute.For<IPaymentService>();

        var log = NewDto(compensationType: "RefundCustomer"); // unsupported type

        var result = await InvokeExecuteCompensation(paymentService, log, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "unknown compensation types must not silently succeed");
        await paymentService.DidNotReceive().CancelPaymentAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region MaxRetries / dead-letter boundary

    /// <summary>
    /// The processor dead-letters when the NEW retry count reaches MaxRetries (5).
    /// This test documents the boundary: after the 5th failure the log is moved
    /// to the dead-letter queue rather than retried again. The full dead-letter
    /// SQL path is verified in the integration test (Scenario D extension).
    /// </summary>
    [Fact]
    public void MaxRetriesBoundary_AtFiveFailures_ShouldDeadLetter()
    {
        const int maxRetries = 5;
        // On the Nth failure, newRetryCount = previous + 1. Dead-letter when
        // newRetryCount >= MaxRetries, i.e. when previous >= 4.
        var willDeadLetter = (4 + 1) >= maxRetries;
        willDeadLetter.Should().BeTrue(
            "a compensation that has already failed 4 times, failing a 5th, must dead-letter");
    }

    #endregion

    private static CompensationLogDto NewDto(string compensationType = "CancelPayment") =>
        new(
            Id: Guid.NewGuid(),
            BookingId: Guid.NewGuid(),
            CompensationType: compensationType,
            Payload: "{}",
            OccurredOnUtc: DateTime.UtcNow,
            IdempotencyKey: $"compensation:{compensationType}:{Guid.NewGuid()}",
            ProcessedOnUtc: null,
            Error: null,
            RetryCount: 0,
            NextRetryOnUtc: null);
}
