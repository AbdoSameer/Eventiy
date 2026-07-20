using System.Net.Http.Json;
using System.Text.Json;
using Eventy.IntegrationTests.Fixtures;
using Eventy.Testing.Foundation.Web;

namespace Eventy.IntegrationTests;

/// <summary>
/// Centralized test data builder — the SINGLE place where test data
/// is created for integration tests.
///
/// Rule of One: change how a Booking or Event is initialized in
/// exactly one place. Every test class consumes this builder.
///
/// Event creation delegates to the fixture's SeedPublishedEventAsync
/// (which calls the domain factory methods with correct invariants).
/// Booking creation goes through the HTTP API to exercise the full
/// handler pipeline (concurrency retry, strategy selection, payment).
/// </summary>
public sealed class TestDataBuilder
{
    private readonly HttpClient _defaultClient;
    private readonly IntegrationTestFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public TestDataBuilder(IntegrationTestFixture fixture)
    {
        _defaultClient = fixture.Client;
        _fixture = fixture;
    }

    /// <summary>
    /// Creates a published event with a single ticket type.
    /// Delegates to the fixture's domain-correct seed method.
    /// Returns the EventId and TicketTypeId.
    /// </summary>
    public Task<(Guid EventId, Guid TicketTypeId)> CreatePublishedEventAsync(
        int eventCapacity = 100,
        int ticketCapacity = 50,
        decimal ticketPrice = 100m,
        string ticketName = "General")
    => _fixture.SeedPublishedEventAsync(eventCapacity, ticketCapacity, ticketPrice);

    /// <summary>
    /// Creates a booking via the HTTP API (handler-driven, domain
    /// invariants enforced). Returns the BookingId.
    /// </summary>
    public async Task<Guid> CreateBookingAsync(
        Guid eventId,
        Guid ticketTypeId,
        int quantity = 1,
        int paymentMethod = 1,
        Guid? userId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/booking")
        {
            Content = JsonContent.Create(new
            {
                eventId,
                ticketTypeId,
                quantity,
                paymentMethod,
            })
        };

        if (userId.HasValue)
            request.Headers.Add("X-Test-UserId", userId.Value.ToString());

        var response = await _defaultClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"CreateBooking failed ({response.StatusCode}): {body}");
        }

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return created.GetProperty("bookingId").GetGuid();
    }

    /// <summary>
    /// Creates multiple bookings via the API. Returns the list of BookingIds.
    /// </summary>
    public async Task<List<Guid>> CreateBookingsAsync(
        Guid eventId,
        Guid ticketTypeId,
        int count,
        int paymentMethod = 1)
    {
        var bookingIds = new List<Guid>();
        for (int i = 0; i < count; i++)
        {
            var userId = Guid.Parse($"77777777-7777-7777-7777-{i:D12}");
            var bookingId = await CreateBookingAsync(
                eventId, ticketTypeId, quantity: 1,
                paymentMethod: paymentMethod, userId: userId);
            bookingIds.Add(bookingId);
        }
        return bookingIds;
    }
}
