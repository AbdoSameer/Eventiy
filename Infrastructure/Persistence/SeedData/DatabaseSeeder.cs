using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Seed
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            try
            {
                // Check if data already exists
                if (await context.Events.AnyAsync())
                {
                    Console.WriteLine("✅ Database already contains data. Skipping seed.");
                    return;
                }

                Console.WriteLine("🌱 Starting database seeding...");
                await SeedEventsAndBookingsAsync(context);
                Console.WriteLine("✅ Database seeded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error seeding database: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        private static async Task SeedEventsAndBookingsAsync(ApplicationDbContext context)
        {
            // Create FIFA World Cup Events
            var events = CreateFifaEvents();

            // Save events first
            foreach (var eventItem in events)
            {
                try
                {
                    await context.Events.AddAsync(eventItem);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ Added event: {eventItem.EventName.Value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to add event {eventItem.EventName.Value}: {ex.Message}");
                    throw;
                }
            }

            // Create bookings for each event
            var bookings = CreateFifaBookings(events);

            if (bookings.Any())
            {
                // Save bookings in batches
                foreach (var booking in bookings)
                {
                    try
                    {
                        await context.Bookings.AddAsync(booking);
                        await context.SaveChangesAsync();
                        Console.WriteLine($"✅ Added booking for event: {booking.EventTitle}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed to add booking for {booking.EventTitle}: {ex.Message}");
                        // Continue with other bookings
                    }
                }
            }

            // Update ticket sales
            await UpdateTicketSalesAsync(context, events);
        }

        private static List<Event> CreateFifaEvents()
        {
            var events = new List<Event>();

            // Helper function to safely get value from Result
            static T GetValue<T>(Result<T> result, string context)
            {
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to create {context}: {string.Join(", ", result.Errors.Select(e => e.Message))}");
                }
                return result.Value;
            }

            try
            {
                // 1. FIFA World Cup Final 2027
                var address1 = GetValue(
                    Address.Create("United States", "New York", "MetLife Stadium", "10001"),
                    "address for FIFA World Cup Final 2027"
                );

                var event1 = GetValue(
                    Event.Create(
                        name: "FIFA World Cup Final 2027",
                        capacity: 75000,
                        date: new DateTime(2027, 7, 19, 18, 0, 0),
                        location: address1,
                        description: "The grand finale of the FIFA World Cup 2027."
                    ),
                    "FIFA World Cup Final 2027"
                );

                var price1 = GetValue(Money.Create(1500, "USD"), "VIP ticket price");
                event1.AddTicketType("VIP Hospitality", price1, 500);

                var price2 = GetValue(Money.Create(800, "USD"), "Premium ticket price");
                event1.AddTicketType("Premium Category 1", price2, 2000);

                var price3 = GetValue(Money.Create(450, "USD"), "Standard ticket price");
                event1.AddTicketType("Standard Category 2", price3, 5000);

                var price4 = GetValue(Money.Create(250, "USD"), "General Admission ticket price");
                event1.AddTicketType("General Admission", price4, 25000);

                event1.Publish();
                events.Add(event1);

                // 2. FIFA World Cup Semi-Final 2027 - Match 1
                var address2 = GetValue(
                    Address.Create("Mexico", "Mexico City", "Estadio Azteca", "01000"),
                    "address for Semi-Final 1"
                );

                var event2 = GetValue(
                    Event.Create(
                        name: "FIFA World Cup Semi-Final 2027 - Match 1",
                        capacity: 68000,
                        date: new DateTime(2027, 7, 14, 17, 0, 0),
                        location: address2,
                        description: "First semi-final of the FIFA World Cup 2027."
                    ),
                    "Semi-Final 1"
                );

                var price5 = GetValue(Money.Create(1200, "USD"), "VIP ticket price");
                event2.AddTicketType("VIP", price5, 400);

                var price6 = GetValue(Money.Create(600, "USD"), "Premium ticket price");
                event2.AddTicketType("Premium", price6, 3000);

                var price7 = GetValue(Money.Create(300, "USD"), "Standard ticket price");
                event2.AddTicketType("Standard", price7, 15000);

                event2.Publish();
                events.Add(event2);

                // 3. FIFA World Cup Semi-Final 2027 - Match 2
                var address3 = GetValue(
                    Address.Create("United States", "Dallas", "AT&T Stadium", "76011"),
                    "address for Semi-Final 2"
                );

                var event3 = GetValue(
                    Event.Create(
                        name: "FIFA World Cup Semi-Final 2027 - Match 2",
                        capacity: 68000,
                        date: new DateTime(2027, 7, 15, 17, 0, 0),
                        location: address3,
                        description: "Second semi-final of the FIFA World Cup 2027."
                    ),
                    "Semi-Final 2"
                );

                var price8 = GetValue(Money.Create(1200, "USD"), "VIP ticket price");
                event3.AddTicketType("VIP", price8, 400);

                var price9 = GetValue(Money.Create(600, "USD"), "Premium ticket price");
                event3.AddTicketType("Premium", price9, 3000);

                var price10 = GetValue(Money.Create(300, "USD"), "Standard ticket price");
                event3.AddTicketType("Standard", price10, 15000);

                event3.Publish();
                events.Add(event3);

                // 4. FIFA World Cup Quarter-Final 2027 - Match 1
                var address4 = GetValue(
                    Address.Create("Canada", "Toronto", "Rogers Centre", "M5V 1J1"),
                    "address for Quarter-Final"
                );

                var event4 = GetValue(
                    Event.Create(
                        name: "FIFA World Cup Quarter-Final 2027 - Match 1",
                        capacity: 55000,
                        date: new DateTime(2027, 7, 9, 16, 0, 0),
                        location: address4,
                        description: "First quarter-final match of the FIFA World Cup 2027."
                    ),
                    "Quarter-Final"
                );

                var price11 = GetValue(Money.Create(900, "USD"), "VIP ticket price");
                event4.AddTicketType("VIP", price11, 300);

                var price12 = GetValue(Money.Create(400, "USD"), "Premium ticket price");
                event4.AddTicketType("Premium", price12, 2000);

                var price13 = GetValue(Money.Create(200, "USD"), "Standard ticket price");
                event4.AddTicketType("Standard", price13, 10000);

                event4.Publish();
                events.Add(event4);

                // 5. FIFA World Cup Group Stage - Brazil vs Argentina
                var address5 = GetValue(
                    Address.Create("United States", "Los Angeles", "SoFi Stadium", "90295"),
                    "address for Brazil vs Argentina"
                );

                var event5 = GetValue(
                    Event.Create(
                        name: "Brazil vs Argentina - Group Stage",
                        capacity: 70000,
                        date: new DateTime(2027, 6, 20, 21, 0, 0),
                        location: address5,
                        description: "An epic South American derby in the group stage."
                    ),
                    "Brazil vs Argentina"
                );

                var price14 = GetValue(Money.Create(1000, "USD"), "VIP ticket price");
                event5.AddTicketType("VIP", price14, 350);

                var price15 = GetValue(Money.Create(500, "USD"), "Premium ticket price");
                event5.AddTicketType("Premium", price15, 2500);

                var price16 = GetValue(Money.Create(250, "USD"), "Standard ticket price");
                event5.AddTicketType("Standard", price16, 20000);

                var price17 = GetValue(Money.Create(150, "USD"), "General Admission ticket price");
                event5.AddTicketType("General Admission", price17, 30000);

                event5.Publish();
                events.Add(event5);

                // 6. FIFA World Cup Group Stage - Spain vs Germany
                var address6 = GetValue(
                    Address.Create("Mexico", "Guadalajara", "Estadio Akron", "45100"),
                    "address for Spain vs Germany"
                );

                var event6 = GetValue(
                    Event.Create(
                        name: "Spain vs Germany - Group Stage",
                        capacity: 65000,
                        date: new DateTime(2027, 6, 22, 18, 0, 0),
                        location: address6,
                        description: "European powerhouses Spain and Germany face off."
                    ),
                    "Spain vs Germany"
                );

                var price18 = GetValue(Money.Create(900, "USD"), "VIP ticket price");
                event6.AddTicketType("VIP", price18, 300);

                var price19 = GetValue(Money.Create(450, "USD"), "Premium ticket price");
                event6.AddTicketType("Premium", price19, 2000);

                var price20 = GetValue(Money.Create(220, "USD"), "Standard ticket price");
                event6.AddTicketType("Standard", price20, 15000);

                event6.Publish();
                events.Add(event6);

                // 7. FIFA World Cup Opening Match 2027
                var address7 = GetValue(
                    Address.Create("Mexico", "Mexico City", "Estadio Azteca", "01000"),
                    "address for Opening Match"
                );

                var event7 = GetValue(
                    Event.Create(
                        name: "FIFA World Cup Opening Match 2027",
                        capacity: 72000,
                        date: new DateTime(2027, 6, 14, 20, 0, 0),
                        location: address7,
                        description: "The grand opening match of the FIFA World Cup 2027."
                    ),
                    "Opening Match"
                );

                var price21 = GetValue(Money.Create(2000, "USD"), "VIP ticket price");
                event7.AddTicketType("VIP Opening Ceremony", price21, 500);

                var price22 = GetValue(Money.Create(700, "USD"), "Premium ticket price");
                event7.AddTicketType("Premium", price22, 3000);

                var price23 = GetValue(Money.Create(350, "USD"), "Standard ticket price");
                event7.AddTicketType("Standard", price23, 20000);

                var price24 = GetValue(Money.Create(200, "USD"), "General Admission ticket price");
                event7.AddTicketType("General Admission", price24, 35000);

                event7.Publish();
                events.Add(event7);

                // 8. Upcoming FIFA Club World Cup 2027 Final (Draft - not published)
                var address8 = GetValue(
                    Address.Create("United States", "Miami", "Hard Rock Stadium", "33056"),
                    "address for Club World Cup"
                );

                var event8 = GetValue(
                    Event.Create(
                        name: "FIFA Club World Cup Final 2027",
                        capacity: 60000,
                        date: new DateTime(2027, 12, 20, 19, 0, 0),
                        location: address8,
                        description: "The FIFA Club World Cup Final."
                    ),
                    "Club World Cup"
                );

                var price25 = GetValue(Money.Create(800, "USD"), "VIP ticket price");
                event8.AddTicketType("VIP", price25, 250);

                var price26 = GetValue(Money.Create(400, "USD"), "Premium ticket price");
                event8.AddTicketType("Premium", price26, 2000);

                var price27 = GetValue(Money.Create(200, "USD"), "Standard ticket price");
                event8.AddTicketType("Standard", price27, 15000);

                // This event remains as Draft (not published)
                events.Add(event8);

                return events;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating events: {ex.Message}");
                throw;
            }
        }

        private static List<Booking> CreateFifaBookings(List<Event> events)
        {
            var bookings = new List<Booking>();
            var random = new Random();

            // Sample user IDs (these would normally come from your Identity system)
            var userIds = new List<UserId>
            {
                UserId.FromDatabase(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                UserId.FromDatabase(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                UserId.FromDatabase(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                UserId.FromDatabase(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                UserId.FromDatabase(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                UserId.FromDatabase(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                UserId.FromDatabase(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                UserId.FromDatabase(Guid.Parse("88888888-8888-8888-8888-888888888888")),
                UserId.FromDatabase(Guid.Parse("99999999-9999-9999-9999-999999999999")),
                UserId.FromDatabase(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
            };

            foreach (var eventItem in events)
            {
                // Only create bookings for published events
                if (eventItem.Status != EventStatus.Published)
                    continue;

                // Get ticket types for this event
                var ticketTypes = eventItem.TicketTypes.ToList();
                if (!ticketTypes.Any())
                    continue;

                // Create 5-15 bookings per event
                int bookingCount = random.Next(5, 15);
                var selectedUserIds = userIds.OrderBy(x => random.Next()).Take(bookingCount).ToList();

                for (int i = 0; i < bookingCount && i < selectedUserIds.Count; i++)
                {
                    var ticketType = ticketTypes[random.Next(ticketTypes.Count)];
                    int quantity = random.Next(1, Math.Min(5, ticketType.Capacity - ticketType.SoldCount + 1));

                    if (quantity <= 0 || ticketType.SoldCount + quantity > ticketType.Capacity)
                        continue;

                    // Create booking with error checking
                    var bookingResult = Booking.Create(
                        userId: selectedUserIds[i],
                        eventId: eventItem.Id,
                        ticketTypeId: ticketType.Id,
                        eventTitle: eventItem.EventName.Value,
                        quantity: quantity,
                        money: ticketType.Price
                    );

                    if (bookingResult.IsFailure)
                    {
                        Console.WriteLine($"Failed to create booking: {string.Join(", ", bookingResult.Errors.Select(e => e.Message))}");
                        continue;
                    }

                    var booking = bookingResult.Value;

                    // Randomly confirm some bookings (70% confirmed)
                    if (random.NextDouble() > 0.3)
                    {
                        booking.Confirm();
                    }

                    // Randomly cancel some confirmed bookings (10% cancelled)
                    if (booking.Status == BookingStatusEnum.Confirmed && random.NextDouble() < 0.1)
                    {
                        booking.Cancel("Changed my mind");
                    }

                    bookings.Add(booking);
                }
            }

            return bookings;
        }

        private static async Task UpdateTicketSalesAsync(ApplicationDbContext context, List<Event> events)
        {
            // Update sold counts for ticket types based on bookings
            foreach (var eventItem in events)
            {
                foreach (var ticketType in eventItem.TicketTypes)
                {
                    try
                    {
                        var bookings = await context.Bookings
                            .Where(b => b.TicketTypeId == ticketType.Id && b.Status == BookingStatusEnum.Confirmed)
                            .ToListAsync();

                        int totalSold = bookings.Sum(b => b.Quantity);

                        // Update the ticket type sold count
                        var ticketTypeToUpdate = await context.Set<TicketType>()
                            .FindAsync(ticketType.Id);

                        if (ticketTypeToUpdate != null)
                        {
                            // Use raw SQL to update SoldCount
                            await context.Database.ExecuteSqlRawAsync(
                                "UPDATE TicketTypes SET SoldCount = {0} WHERE Id = {1}",
                                totalSold, ticketTypeToUpdate.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating ticket sales for {ticketType.TicketTypeName}: {ex.Message}");
                    }
                }
            }
        }
    }
}