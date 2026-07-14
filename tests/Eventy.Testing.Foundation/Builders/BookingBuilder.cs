using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

namespace Eventy.Testing.Foundation.Builders;

/// <summary>
/// Fluent builder for creating valid <see cref="Booking"/> aggregates in tests.
/// </summary>
public class BookingBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _eventId = Guid.NewGuid();
    private Guid _ticketTypeId = Guid.NewGuid();
    private string _eventTitle = "Test Event";
    private int _quantity = 1;
    private decimal _amount = 100.00m;
    private string _currency = "EGP";
    private PaymentMethod _paymentMethod = PaymentMethod.Instant;

    public BookingBuilder WithUserId(Guid userId) { _userId = userId; return this; }
    public BookingBuilder WithEventId(Guid eventId) { _eventId = eventId; return this; }
    public BookingBuilder WithTicketTypeId(Guid ticketTypeId) { _ticketTypeId = ticketTypeId; return this; }
    public BookingBuilder WithEventTitle(string title) { _eventTitle = title; return this; }
    public BookingBuilder WithQuantity(int quantity) { _quantity = quantity; return this; }
    public BookingBuilder WithAmount(decimal amount) { _amount = amount; return this; }
    public BookingBuilder WithCurrency(string currency) { _currency = currency; return this; }
    public BookingBuilder WithPaymentMethod(PaymentMethod method) { _paymentMethod = method; return this; }

    /// <summary>
    /// Builds a valid pending Booking via the domain factory method.
    /// </summary>
    public Result<Booking> Build()
    {
        return Booking.Create(
            UserId.FromDatabase(_userId),
            EventId.FromDatabase(_eventId),
            TicketTypeId.FromDatabase(_ticketTypeId),
            _eventTitle,
            _quantity,
            Money.FromDecimal(_amount, _currency).Value,
            _paymentMethod,
            DateTime.UtcNow);
    }
}
