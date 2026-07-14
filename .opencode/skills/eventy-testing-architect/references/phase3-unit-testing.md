# Phase 3 — Domain & Application Unit Testing

Unit tests validate business correctness without infrastructure. No database, no mocking framework for domain, no MediatR, no HTTP.

## Table of Contents
1. [Testing Priority](#testing-priority)
2. [Domain Aggregate Tests](#domain-aggregate-tests)
3. [Value Object Tests](#value-object-tests)
4. [Domain Event Tests](#domain-event-tests)
5. [Command Handler Tests](#command-handler-tests)
6. [Pipeline Behavior Tests](#pipeline-behavior-tests)
7. [Mocking Strategy](#mocking-strategy)
8. [Folder Structure](#folder-structure)
9. [Quality Rules](#quality-rules)

## Testing Priority

```
1. Domain Aggregates    ← Most important — protect business invariants
2. Value Objects
3. Domain Events
4. Application Commands
5. Pipeline Behaviors
6. Application Services
```

If the Domain is incorrect, every higher layer will also be incorrect.

## Domain Aggregate Tests

For every Aggregate Root, create a test map covering: Creation, State Transition, Business Rules, Domain Events, Invalid Operations.

### Invariant Tests (Highest Priority)

```csharp
[Fact]
public void Reserve_WhenCapacityExceeded_ShouldReturnFailure()
{
    // Arrange
    var ticket = TicketType.Create("VIP", capacity: 100);
    ticket.Reserve(50); // 50 remaining

    // Act
    var result = ticket.Reserve(51); // Try to reserve 51

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Should().Be(TicketErrors.CapacityExceeded);
}
```

### State Machine Tests

Booking lifecycle: Pending → Confirmed → Cancelled

```csharp
[Fact]
public void Confirm_WhenBookingIsPending_ShouldSucceed()
{
    var booking = Booking.Create(userId, ticketId, quantity: 2);

    var result = booking.Confirm();

    result.IsSuccess.Should().BeTrue();
    booking.Status.Should().Be(BookingStatus.Confirmed);
}

[Fact]
public void Confirm_WhenBookingIsCancelled_ShouldReturnFailure()
{
    var booking = Booking.Create(userId, ticketId, quantity: 2);
    booking.Cancel();

    var result = booking.Confirm();

    result.IsFailure.Should().BeTrue();
    result.Error.Should().Be(BookingErrors.InvalidStateTransition);
}
```

### Aggregate Test Categories

| Category | What to Test | Example |
|----------|-------------|---------|
| Creation | Valid/invalid construction | `Create_WithEmptyName_ShouldFail` |
| Invariants | Business rules that must always hold | `Reserve_WhenSoldOut_ShouldFail` |
| State transitions | Valid/invalid lifecycle changes | `Confirm_WhenPending_ShouldSucceed` |
| Domain events | Events raised on state changes | `Confirm_ShouldRaiseBookingConfirmedEvent` |
| Invalid operations | Operations that should never succeed | `Cancel_WhenAlreadyConfirmedPayment_ShouldFail` |

## Value Object Tests

Test: Creation, Validation, Equality, Conversion

```csharp
[Fact]
public void Create_WithInvalidEmail_ShouldReturnFailure()
{
    var result = Email.Create("not-an-email");
    result.IsFailure.Should().BeTrue();
}

[Fact]
public void SameValue_ShouldBeEqual()
{
    var email1 = Email.Create("test@eventy.com").Value;
    var email2 = Email.Create("test@eventy.com").Value;

    email1.Should().Be(email2);
    email1.GetHashCode().Should().Be(email2.GetHashCode());
}
```

Value objects must be immutable. Never mutate after creation — create new instances instead.

## Domain Event Tests

Ensure business actions produce correct events with correct data:

```csharp
[Fact]
public void ConfirmBooking_ShouldRaiseBookingConfirmedDomainEvent()
{
    var booking = Booking.Create(userId, ticketId, quantity: 2);
    booking.ClearDomainEvents();

    booking.Confirm();

    var domainEvent = booking.DomainEvents
        .Should().ContainSingle()
        .Subject.Should().BeOfType<BookingConfirmedDomainEvent>();

    domainEvent.BookingId.Should().Be(booking.Id);
    domainEvent.UserId.Should().Be(userId);
    domainEvent.EventId.Should().Be(ticketId);
}
```

Good event names describe business facts: `BookingCreated`, `TicketReserved`, `PaymentCompleted`. Bad names: `BookingServiceExecuted`, `DatabaseUpdated`.

## Command Handler Tests

Verify: Input (command data), Dependencies (repository responses), Output (Result), Side Effects (saved entities/events).

```csharp
[Fact]
public async Task Handle_WhenTicketAvailable_ShouldCreateBooking()
{
    // Arrange
    var ticket = TicketType.Create("VIP", capacity: 10);
    var command = new CreateBookingCommand(TicketId: ticket.Id, Quantity: 2);

    _ticketRepo.GetAsync(ticket.Id).Returns(ticket);
    _unitOfWork.SaveChangesAsync().Returns(1);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    await _bookingRepo.Received(1).AddAsync(Arg.Any<Booking>());
    await _unitOfWork.Received(1).SaveChangesAsync();
}
```

### Mock Setup Pattern

```csharp
public class CreateBookingHandlerTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ITicketTypeRepository _ticketRepo = Substitute.For<ITicketTypeRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateBookingHandler _handler;

    public CreateBookingHandlerTests()
    {
        _handler = new CreateBookingHandler(_bookingRepo, _ticketRepo, _unitOfWork);
    }
}
```

## Pipeline Behavior Tests

Test MediatR pipeline behaviors in isolation:

```csharp
[Fact]
public async Task Handle_WhenValidationFails_ShouldNotCallHandler()
{
    var command = new CreateBookingCommand(); // Invalid — missing required fields
    var behavior = new ValidationBehavior<CreateBookingCommand, Result>(
        new[] { new CreateBookingValidator() });

    var result = await behavior.Handle(command, () => throw new Exception("Should not reach"), CancellationToken.None);

    result.IsFailure.Should().BeTrue();
}
```

## Mocking Strategy

- **Mock interfaces, not classes**: `IBookingRepository`, `IUnitOfWork`, `ICurrentUserService`, `IPaymentService`, `IOutboxRepository`
- **Avoid over-mocking**: Test important behavior (was booking created? was failure returned?) not every method call
- **Use NSubstitute** for mocking, FluentAssertions for assertions

Common mock interfaces:

```csharp
IBookingRepository
IEventRepository
ITicketTypeRepository
IUnitOfWork
ICurrentUserService
IPaymentService
IOutboxRepository
ICacheService
```

## Folder Structure

```
tests/
├── Eventy.Domain.UnitTests/
│   ├── Aggregates/
│   │   ├── BookingTests.cs
│   │   ├── EventTests.cs
│   │   └── TicketTypeTests.cs
│   ├── ValueObjects/
│   │   ├── EmailTests.cs
│   │   └── MoneyTests.cs
│   └── DomainEvents/
│       └── BookingDomainEventTests.cs
│
└── Eventy.Application.UnitTests/
    ├── Features/
    │   └── Bookings/
    │       └── Commands/
    │           ├── CreateBookingHandlerTests.cs
    │           └── CancelBookingHandlerTests.cs
    ├── Behaviors/
    │   ├── ValidationBehaviorTests.cs
    │   └── LoggingBehaviorTests.cs
    └── Builders/
        └── DomainTestDataBuilder.cs
```

## Quality Rules

| Rule | Guideline |
|------|-----------|
| One behavior per test | `CreateBooking_ShouldCreateBooking` not `TestCreateBookingAndPaymentAndEmail` |
| Naming | `Method_WhenCondition_ShouldExpectedResult` |
| No private method testing | Test public domain behavior only |
| Self-documenting | Test name should explain business rule without reading production code |
| Pure unit tests | No DB, no HTTP, no Docker — if it needs these, it's an integration test |
| Fast execution | All unit tests must complete in under 10 seconds |

## Completion Criteria

- Every Aggregate has invariant protection tests
- Every Value Object has validation/equality tests
- Every Command Handler has success + failure scenarios
- Every Pipeline Behavior has isolated tests
- No database dependency exists
- Tests execute quickly (< 10 seconds for the full suite)
