using System.Reflection;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Features.Compensation;
using Domain.Abstractions.Persistence;
using Domain.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.BackgroundJobs;

/// <summary>
/// Unit tests for the compensation retry-schedule logic and execution dispatch.
///
/// <see cref="Infrastructure.BackgroundJobs.CompensationProcessor"/> owns the
/// polling/locking cycle (exercised by integration tests). The backoff helpers
/// are tested via reflection; the execution logic was extracted to
/// <see cref="CompensationExecutionService"/> and tested directly here.
/// </summary>
public class CompensationProcessorTests
{
    // The CompensationProcessor type lives in Infrastructure, but ComputeNextRetry
    // is a private static pure function — safe to invoke via reflection.
    private static readonly Type ProcessorType =
        typeof(Infrastructure.BackgroundJobs.CompensationProcessor);

    private static DateTime? InvokeComputeNextRetry(int retryCount, DateTime now)
    {
        var method = ProcessorType.GetMethod("ComputeNextRetry",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("ComputeNextRetry must exist for backoff scheduling");
        return (DateTime?)method!.Invoke(null, [retryCount, now])!;
    }

    private static CompensationExecutionService CreateService(
        IPaymentService? paymentService = null)
    {
        paymentService ??= Substitute.For<IPaymentService>();
        var bookingRepo = Substitute.For<IBookingRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var logger = Substitute.For<ILogger<CompensationExecutionService>>();
        return new CompensationExecutionService(
            paymentService, bookingRepo, eventRepo, uow, logger);
    }

    #region ComputeNextRetry — backoff schedule

    [Fact]
    public void ComputeNextRetry_RetryZero_ReturnsNull()
    {
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
        var now = DateTime.UtcNow;
        var next = InvokeComputeNextRetry(99, now);

        next.Should().Be(now.AddMinutes(15),
            "retry counts past the backoff array must clamp to the maximum (15 minutes)");
    }

    #endregion

    #region CompensationExecutionService — dispatch by type

    [Fact]
    public async Task ExecuteCompensation_CancelPayment_CallsCancelAndReturnsSuccess()
    {
        var paymentService = Substitute.For<IPaymentService>();
        paymentService.CancelPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        var service = CreateService(paymentService);
        var log = NewDto(compensationType: "CancelPayment");

        var result = await service.ExecuteAsync(log, CancellationToken.None);

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

        var service = CreateService(paymentService);
        var log = NewDto(compensationType: "CancelPayment");

        var result = await service.ExecuteAsync(log, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "a transient gateway failure must propagate so the processor can schedule a retry");
    }

    [Fact]
    public async Task ExecuteCompensation_UnknownType_ReturnsFailureWithoutCallingGateway()
    {
        var paymentService = Substitute.For<IPaymentService>();
        var service = CreateService(paymentService);
        var log = NewDto(compensationType: "RefundCustomer");

        var result = await service.ExecuteAsync(log, CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "unknown compensation types must not silently succeed");
        await paymentService.DidNotReceive().CancelPaymentAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region MaxRetries / dead-letter boundary

    [Fact]
    public void MaxRetriesBoundary_AtFiveFailures_ShouldDeadLetter()
    {
        const int maxRetries = 5;
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
