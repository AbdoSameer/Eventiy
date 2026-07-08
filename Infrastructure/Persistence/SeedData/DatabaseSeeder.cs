using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting database seeding...");
            var userIds = await SeedUsersAsync(context, logger);

            if (await context.Events.AnyAsync())
            {
                logger.LogInformation("Database already contains event data. Skipping event and booking seed.");
                return;
            }

            await SeedEventsAndBookingsAsync(context, userIds, logger);
            logger.LogInformation("Database seeded successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding database: {Message}", ex.Message);
            throw;
        }
    }

    private static async Task<List<UserId>> SeedUsersAsync(
        ApplicationDbContext context,
        ILogger logger)
    {
        var passwordHasher = new PasswordHasher();
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;
        var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);
        var userIds = new List<UserId>();

        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist. Skipping user seed.");
            return await context.Users.Select(u => u.Id).ToListAsync();
        }

        static User CreateUser(string email, string password, Role role, bool isApproved,
            string firstName, string lastName, PasswordHasher hasher,
            DateTime utcNow, EventMetadata em)
        {
            var emailResult = Email.Create(email);
            if (emailResult.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create email {email}: {string.Join(", ", emailResult.Errors.Select(e => e.Message))}");

            var userResult = User.Create(
                firstName,
                lastName,
                emailResult.Value,
                hasher.Hash(password),
                role,
                utcNow,
                em,
                isApproved);

            if (userResult.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create user {email}: {string.Join(", ", userResult.Errors.Select(e => e.Message))}");

            return userResult.Value;
        }

        // Admin
        var admin = CreateUser("abdo@eventiy.local", "Abdo@12345", Role.Admin, true, "Abdo", "Admin", passwordHasher, utcNow, metadata);
        context.Users.Add(admin);
        userIds.Add(admin.Id);
        logger.LogInformation("Seeded admin user: abdo@eventiy.local");

        // Organizer (approved)
        var organizer = CreateUser("organizer@eventiy.local", "Org@12345", Role.Organizer, true,
            "Ahmed", "Organizer", passwordHasher, utcNow, metadata);
        context.Users.Add(organizer);
        userIds.Add(organizer.Id);
        logger.LogInformation("Seeded organizer: organizer@eventiy.local");

        // Organizer (pending � not yet approved)
        var pendingOrg = CreateUser("pending@eventiy.local", "Pending@12345", Role.Organizer, false,
            "Tariq", "Pending", passwordHasher, utcNow, metadata);
        context.Users.Add(pendingOrg);
        userIds.Add(pendingOrg.Id);
        logger.LogInformation("Seeded pending organizer: pending@eventiy.local");

        // 10 Attendees
        for (int i = 1; i <= 10; i++)
        {
            var attendee = CreateUser($"attendee{i}@eventiy.local", "Attendee@12345",
                Role.Attendee, true, $"Attendee{i}", $"User{i}",
                passwordHasher, utcNow, metadata);
            context.Users.Add(attendee);
            userIds.Add(attendee.Id);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} users total", userIds.Count);

        return userIds;
    }

    private static async Task SeedEventsAndBookingsAsync(
        ApplicationDbContext context,
        List<UserId> userIds,
        ILogger logger)
    {
        var events = CreateFifaEvents(logger);

        foreach (var eventItem in events)
        {
            try
            {
                await context.Events.AddAsync(eventItem);
                await context.SaveChangesAsync();
                logger.LogInformation("Added event: {EventName}", eventItem.EventName.Value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add event {EventName}: {Message}",
                    eventItem.EventName.Value, ex.Message);
                throw;
            }
        }

        var attendeeIds = userIds.Skip(3).ToList();
        var bookings = CreateFifaBookings(events, attendeeIds, logger);

        if (bookings.Any())
        {
            foreach (var booking in bookings)
            {
                try
                {
                    await context.Bookings.AddAsync(booking);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Added booking for event: {EventTitle}", booking.EventTitle);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to add booking for {EventTitle}: {Message}",
                        booking.EventTitle, ex.Message);
                }
            }
        }

        await UpdateTicketSalesAsync(context, events, logger);
    }

    private static List<Event> CreateFifaEvents(ILogger logger)
    {
        var events = new List<Event>();
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;
        var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

        static T GetValue<T>(Result<T> result, string ctx)
        {
            if (result.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create {ctx}: {string.Join(", ", result.Errors.Select(e => e.Message))}");
            return result.Value;
        }

        try
        {
            // 1. FIFA World Cup Final 2027
            var address1 = GetValue(
                Address.Create("United States", "New York", "MetLife Stadium", "10001"),
                "address for FIFA World Cup Final 2027");

            var event1 = GetValue(
                Event.Create("FIFA World Cup Final 2027", 75000,
                    new DateTime(2027, 7, 19, 18, 0, 0),
                    address1, "The grand finale of the FIFA World Cup 2027.",
                    EventType.Sports, utcNow, metadata),
                "FIFA World Cup Final 2027");

            event1.AddTicketType("VIP Hospitality",
                GetValue(Money.Create(1500, "USD"), "VIP ticket price"), 500, utcNow, metadata);
            event1.AddTicketType("Premium Category 1",
                GetValue(Money.Create(800, "USD"), "Premium ticket price"), 2000, utcNow, metadata);
            event1.AddTicketType("Standard Category 2",
                GetValue(Money.Create(450, "USD"), "Standard ticket price"), 5000, utcNow, metadata);
            event1.AddTicketType("General Admission",
                GetValue(Money.Create(250, "USD"), "General Admission ticket price"), 25000, utcNow, metadata);
            event1.Publish(utcNow, metadata);
            events.Add(event1);

            // 2. Semi-Final 1
            var address2 = GetValue(
                Address.Create("Mexico", "Mexico City", "Estadio Azteca", "01000"),
                "address for Semi-Final 1");

            var event2 = GetValue(
                Event.Create("FIFA World Cup Semi-Final 2027 - Match 1", 68000,
                    new DateTime(2027, 7, 14, 17, 0, 0),
                    address2, "First semi-final of the FIFA World Cup 2027.",
                    EventType.Sports, utcNow, metadata),
                "Semi-Final 1");

            event2.AddTicketType("VIP",
                GetValue(Money.Create(1200, "USD"), "VIP ticket price"), 400, utcNow, metadata);
            event2.AddTicketType("Premium",
                GetValue(Money.Create(600, "USD"), "Premium ticket price"), 3000, utcNow, metadata);
            event2.AddTicketType("Standard",
                GetValue(Money.Create(300, "USD"), "Standard ticket price"), 15000, utcNow, metadata);
            event2.Publish(utcNow, metadata);
            events.Add(event2);

            // 3. Semi-Final 2
            var address3 = GetValue(
                Address.Create("United States", "Dallas", "AT&T Stadium", "76011"),
                "address for Semi-Final 2");

            var event3 = GetValue(
                Event.Create("FIFA World Cup Semi-Final 2027 - Match 2", 68000,
                    new DateTime(2027, 7, 15, 17, 0, 0),
                    address3, "Second semi-final of the FIFA World Cup 2027.",
                    EventType.Sports, utcNow, metadata),
                "Semi-Final 2");

            event3.AddTicketType("VIP",
                GetValue(Money.Create(1200, "USD"), "VIP ticket price"), 400, utcNow, metadata);
            event3.AddTicketType("Premium",
                GetValue(Money.Create(600, "USD"), "Premium ticket price"), 3000, utcNow, metadata);
            event3.AddTicketType("Standard",
                GetValue(Money.Create(300, "USD"), "Standard ticket price"), 15000, utcNow, metadata);
            event3.Publish(utcNow, metadata);
            events.Add(event3);

            // 4. Quarter-Final
            var address4 = GetValue(
                Address.Create("Canada", "Toronto", "Rogers Centre", "M5V 1J1"),
                "address for Quarter-Final");

            var event4 = GetValue(
                Event.Create("FIFA World Cup Quarter-Final 2027 - Match 1", 55000,
                    new DateTime(2027, 7, 9, 16, 0, 0),
                    address4, "First quarter-final match of the FIFA World Cup 2027.",
                    EventType.Sports, utcNow, metadata),
                "Quarter-Final");

            event4.AddTicketType("VIP",
                GetValue(Money.Create(900, "USD"), "VIP ticket price"), 300, utcNow, metadata);
            event4.AddTicketType("Premium",
                GetValue(Money.Create(400, "USD"), "Premium ticket price"), 2000, utcNow, metadata);
            event4.AddTicketType("Standard",
                GetValue(Money.Create(200, "USD"), "Standard ticket price"), 10000, utcNow, metadata);
            event4.Publish(utcNow, metadata);
            events.Add(event4);

            // 5. Brazil vs Argentina
            var address5 = GetValue(
                Address.Create("United States", "Los Angeles", "SoFi Stadium", "90295"),
                "address for Brazil vs Argentina");

            var event5 = GetValue(
                Event.Create("Brazil vs Argentina - Group Stage", 70000,
                    new DateTime(2027, 6, 20, 21, 0, 0),
                    address5, "An epic South American derby in the group stage.",
                    EventType.Sports, utcNow, metadata),
                "Brazil vs Argentina");

            event5.AddTicketType("VIP",
                GetValue(Money.Create(1000, "USD"), "VIP ticket price"), 350, utcNow, metadata);
            event5.AddTicketType("Premium",
                GetValue(Money.Create(500, "USD"), "Premium ticket price"), 2500, utcNow, metadata);
            event5.AddTicketType("Standard",
                GetValue(Money.Create(250, "USD"), "Standard ticket price"), 20000, utcNow, metadata);
            event5.AddTicketType("General Admission",
                GetValue(Money.Create(150, "USD"), "General Admission ticket price"), 30000, utcNow, metadata);
            event5.Publish(utcNow, metadata);
            events.Add(event5);

            // 6. Spain vs Germany
            var address6 = GetValue(
                Address.Create("Mexico", "Guadalajara", "Estadio Akron", "45100"),
                "address for Spain vs Germany");

            var event6 = GetValue(
                Event.Create("Spain vs Germany - Group Stage", 65000,
                    new DateTime(2027, 6, 22, 18, 0, 0),
                    address6, "European powerhouses Spain and Germany face off.",
                    EventType.Sports, utcNow, metadata),
                "Spain vs Germany");

            event6.AddTicketType("VIP",
                GetValue(Money.Create(900, "USD"), "VIP ticket price"), 300, utcNow, metadata);
            event6.AddTicketType("Premium",
                GetValue(Money.Create(450, "USD"), "Premium ticket price"), 2000, utcNow, metadata);
            event6.AddTicketType("Standard",
                GetValue(Money.Create(220, "USD"), "Standard ticket price"), 15000, utcNow, metadata);
            event6.Publish(utcNow, metadata);
            events.Add(event6);

            // 7. Opening Match
            var address7 = GetValue(
                Address.Create("Mexico", "Mexico City", "Estadio Azteca", "01000"),
                "address for Opening Match");

            var event7 = GetValue(
                Event.Create("FIFA World Cup Opening Match 2027", 72000,
                    new DateTime(2027, 6, 14, 20, 0, 0),
                    address7, "The grand opening match of the FIFA World Cup 2027.",
                    EventType.Sports, utcNow, metadata),
                "Opening Match");

            event7.AddTicketType("VIP Opening Ceremony",
                GetValue(Money.Create(2000, "USD"), "VIP ticket price"), 500, utcNow, metadata);
            event7.AddTicketType("Premium",
                GetValue(Money.Create(700, "USD"), "Premium ticket price"), 3000, utcNow, metadata);
            event7.AddTicketType("Standard",
                GetValue(Money.Create(350, "USD"), "Standard ticket price"), 20000, utcNow, metadata);
            event7.AddTicketType("General Admission",
                GetValue(Money.Create(200, "USD"), "General Admission ticket price"), 35000, utcNow, metadata);
            event7.Publish(utcNow, metadata);
            events.Add(event7);

            // 8. Club World Cup (Draft)
            var address8 = GetValue(
                Address.Create("United States", "Miami", "Hard Rock Stadium", "33056"),
                "address for Club World Cup");

            var event8 = GetValue(
                Event.Create("FIFA Club World Cup Final 2027", 60000,
                    new DateTime(2027, 12, 20, 19, 0, 0),
                    address8, "The FIFA Club World Cup Final.",
                    EventType.Sports, utcNow, metadata),
                "Club World Cup");

            event8.AddTicketType("VIP",
                GetValue(Money.Create(800, "USD"), "VIP ticket price"), 250, utcNow, metadata);
            event8.AddTicketType("Premium",
                GetValue(Money.Create(400, "USD"), "Premium ticket price"), 2000, utcNow, metadata);
            event8.AddTicketType("Standard",
                GetValue(Money.Create(200, "USD"), "Standard ticket price"), 15000, utcNow, metadata);
            // Draft � not published
            events.Add(event8);

            return events;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating events: {Message}", ex.Message);
            throw;
        }
    }

    private static List<Booking> CreateFifaBookings(
        List<Event> events,
        List<UserId> attendeeIds,
        ILogger logger)
    {
        var bookings = new List<Booking>();
        var random = new Random();
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;
        var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

        foreach (var eventItem in events)
        {
            if (eventItem.Status != EventStatus.Published)
                continue;

            var ticketTypes = eventItem.TicketTypes.ToList();
            if (!ticketTypes.Any())
                continue;

            int bookingCount = random.Next(5, attendeeIds.Count + 1);
            var selectedUserIds = attendeeIds.OrderBy(_ => random.Next()).Take(bookingCount).ToList();

            foreach (var userId in selectedUserIds)
            {
                var ticketType = ticketTypes[random.Next(ticketTypes.Count)];
                int quantity = random.Next(1, 5);

                var bookingResult = Booking.Create(
                    userId, eventItem.Id, ticketType.Id,
                    eventItem.EventName.Value, quantity, ticketType.Price,
                    PaymentMethod.Instant, utcNow, metadata);

                if (bookingResult.IsFailure)
                {
                    logger.LogWarning("Failed to create booking: {Errors}",
                        string.Join(", ", bookingResult.Errors.Select(e => e.Message)));
                    continue;
                }

                var booking = bookingResult.Value;

                if (random.NextDouble() > 0.3)
                    booking.Confirm(utcNow, metadata);

                if (booking.Status == BookingStatusEnum.Confirmed && random.NextDouble() < 0.1)
                    booking.Cancel(utcNow, metadata, "Changed my mind");

                bookings.Add(booking);
            }
        }

        return bookings;
    }

    private static async Task UpdateTicketSalesAsync(
        ApplicationDbContext context,
        List<Event> events,
        ILogger logger)
    {
        foreach (var eventItem in events)
        {
            foreach (var ticketType in eventItem.TicketTypes)
            {
                try
                {
                    var bookings = await context.Bookings
                        .Where(b => b.TicketTypeId == ticketType.Id
                                 && b.Status == BookingStatusEnum.Confirmed)
                        .ToListAsync();

                    int totalSold = bookings.Sum(b => b.Quantity);

                    var ticketTypeToUpdate = await context.Set<TicketType>().FindAsync(ticketType.Id);

                    if (ticketTypeToUpdate is not null)
                    {
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE TicketTypes SET SoldCount = {0} WHERE Id = {1}",
                            totalSold, ticketTypeToUpdate.Id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error updating ticket sales for {TicketTypeName}: {Message}",
                        ticketType.TicketTypeName, ex.Message);
                }
            }
        }
    }
}
