using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;

namespace Eventy.Testing.Foundation.Builders;

/// <summary>
/// Fluent builder for creating valid <see cref="Event"/> aggregates in tests.
/// Never use <c>new Event()</c> in tests — always go through the builder.
/// </summary>
public class EventBuilder
{
    private string _name = "Test Event";
    private int _capacity = 100;
    private DateTime _date = DateTime.UtcNow.AddDays(30);
    private Address _location = Address.Create("Egypt", "Cairo", "Nile City", "11511", latitude: 30.0444, longitude: 31.2357).Value;
    private string _description = "A test event for unit testing.";
    private EventType _type = EventType.Music;

    public EventBuilder WithName(string name) { _name = name; return this; }
    public EventBuilder WithCapacity(int capacity) { _capacity = capacity; return this; }
    public EventBuilder WithDate(DateTime date) { _date = date; return this; }
    public EventBuilder WithLocation(Address location) { _location = location; return this; }
    public EventBuilder WithDescription(string description) { _description = description; return this; }
    public EventBuilder WithType(EventType type) { _type = type; return this; }

    /// <summary>
    /// Builds a valid Event via the domain factory method.
    /// </summary>
    public Result<Event> Build()
    {
        return Event.Create(_name, _capacity, _date, _location, _description, _type, DateTime.UtcNow);
    }

    /// <summary>
    /// Builds a published (ready-for-booking) event with a default ticket type.
    /// </summary>
    public Result<Event> BuildPublishedWithTickets(string ticketName = "General", decimal price = 50.00m, int ticketCapacity = 50)
    {
        var eventResult = Build();
        if (eventResult.IsFailure) return eventResult;

        var @event = eventResult.Value;
        var utcNow = DateTime.UtcNow;
        @event.Publish(utcNow);
        @event.AddTicketType(ticketName, Money.FromDecimal(price, "EGP").Value, ticketCapacity, utcNow);

        return Result<Event>.Success(@event);
    }
}
