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
        var userIds = new List<UserId>();

        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("Users already exist. Skipping user seed.");
            return await context.Users.Select(u => u.Id).ToListAsync();
        }

        static User CreateUser(string email, string password, Role role, bool isApproved,
            string firstName, string lastName, PasswordHasher hasher,
            DateTime utcNow)
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
                isApproved);

            if (userResult.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create user {email}: {string.Join(", ", userResult.Errors.Select(e => e.Message))}");

            return userResult.Value;
        }

        // Admin
        var admin = CreateUser("abdo@eventiy.local", "Abdo@12345", Role.Admin, true, "Abdo", "Admin", passwordHasher, utcNow);
        context.Users.Add(admin);
        userIds.Add(admin.Id);
        logger.LogInformation("Seeded admin user: abdo@eventiy.local");

        // Organizer (approved)
        var organizer = CreateUser("organizer@eventiy.local", "Org@12345", Role.Organizer, true,
            "Ahmed", "Organizer", passwordHasher, utcNow);
        context.Users.Add(organizer);
        userIds.Add(organizer.Id);
        logger.LogInformation("Seeded organizer: organizer@eventiy.local");

        // Organizer (pending � not yet approved)
        var pendingOrg = CreateUser("pending@eventiy.local", "Pending@12345", Role.Organizer, false,
            "Tariq", "Pending", passwordHasher, utcNow);
        context.Users.Add(pendingOrg);
        userIds.Add(pendingOrg.Id);
        logger.LogInformation("Seeded pending organizer: pending@eventiy.local");

        // 10 Attendees
        for (int i = 1; i <= 10; i++)
        {
            var attendee = CreateUser($"attendee{i}@eventiy.local", "Attendee@12345",
                Role.Attendee, true, $"Attendee{i}", $"User{i}",
                passwordHasher, utcNow);
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
        var events = CreateSeedEvents(logger);

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
        var bookings = CreateSeedBookings(events, attendeeIds, logger);

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

    private static List<Event> CreateSeedEvents(ILogger logger)
    {
        var events = new List<Event>();
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;

        static T GetValue<T>(Result<T> result, string ctx)
        {
            if (result.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create {ctx}: {string.Join(", ", result.Errors.Select(e => e.Message))}");
            return result.Value;
        }

        // ─── Venue zone definitions — maps TicketType → SVG section ─────
        static void AddVenueTicketTypes(
            Event evt,
            (string name, string sectionCode, decimal price, int capacity)[] zones,
            string venueType,
            DateTime utcNow)
        {
            foreach (var (name, sectionCode, price, capacity) in zones)
            {
                var money = GetValue(Money.Create(price, "USD"), $"money for {name}");
                evt.AddTicketType(name, money, capacity, utcNow, sectionCode, venueType);
            }
        }

        var ConcertZones = new[]
        {
            ("Front Pit", "FP1", 1250m, 300),
            ("Front Pit (Side)", "FP2", 1100m, 240),
            ("Main Floor", "MF1", 750m, 600),
            ("Main Floor (Rear)", "MF2", 600m, 540),
            ("Side Bowl", "SB1", 350m, 900),
            ("Side Bowl (Upper)", "SB2", 280m, 820),
            ("Rear Bowl", "REAR", 220m, 700),
            ("VIP Suite", "VIP1", 2500m, 40),
            ("VIP Balcony", "VIP2", 1850m, 32),
        };

        var SportZones = new[]
        {
            ("Standard Bowl", "116", 2123m, 1200),
            ("Standard Bowl", "124L", 2294m, 980),
            ("Standard Corner", "234", 3395m, 760),
            ("Standard Side", "S105", 2540m, 1100),
            ("Standard Side", "S118", 2675m, 1040),
            ("Premium Club", "S180", 4700m, 420),
            ("Premium Lounge", "C129", 6850m, 260),
            ("Premium Corner Suite", "GC19", 7956m, 180),
            ("VVIP Premium Box", "VVIP1", 81758m, 24),
            ("VVIP Sideline Suite", "VVIP2", 54300m, 16),
        };

        var TheaterZones = new[]
        {
            ("Orchestra Center", "ORCH", 480m, 500),
            ("Orchestra Left", "ORCHL", 380m, 320),
            ("Orchestra Right", "ORCHR", 380m, 320),
            ("Mezzanine", "MEZZ", 280m, 280),
            ("Balcony", "BALC", 180m, 240),
            ("Box Suite (Left)", "BOXL", 950m, 12),
            ("Box Suite (Right)", "BOXR", 950m, 12),
            ("Front Row", "FRONT", 650m, 30),
        };

        try
        {
            // ─── Music ──────────────────────────────────────────────
            var addrMusic1 = GetValue(
                Address.Create("Egypt", "Cairo", "Cairo International Stadium", "11511"),
                "address for Cairo Music Festival");
            var music1 = GetValue(
                Event.Create("Cairo International Music Festival 2027", 60000,
                    new DateTime(2027, 7, 20, 19, 0, 0),
                    addrMusic1, "Three nights of world-class performances at the historic Cairo Stadium, featuring international and Arab artists.",
                    EventType.Music, utcNow),
                "Cairo Music Festival");
            AddVenueTicketTypes(music1, ConcertZones, "Concert", utcNow);
            music1.Publish(utcNow);
            events.Add(music1);

            var addrMusic2 = GetValue(
                Address.Create("France", "Paris", "Accor Arena", "75012"),
                "address for Paris Jazz Festival");
            var music2 = GetValue(
                Event.Create("Paris Jazz Festival 2027", 18000,
                    new DateTime(2027, 6, 5, 18, 0, 0),
                    addrMusic2, "World-renowned jazz musicians converge at the iconic Accor Arena for a week-long celebration of improvisation and rhythm.",
                    EventType.Music, utcNow),
                "Paris Jazz Festival");
            AddVenueTicketTypes(music2, ConcertZones, "Concert", utcNow);
            music2.Publish(utcNow);
            events.Add(music2);

            // ─── Tech ───────────────────────────────────────────────
            var addrTech1 = GetValue(
                Address.Create("United Arab Emirates", "Dubai", "Dubai World Trade Centre", "00000"),
                "address for Dubai AI Summit");
            var tech1 = GetValue(
                Event.Create("Dubai AI & Blockchain Summit 2027", 40000,
                    new DateTime(2027, 10, 12, 9, 0, 0),
                    addrTech1, "The Middle East's largest technology summit exploring AI, blockchain, fintech, and Web3 innovation.",
                    EventType.Tech, utcNow),
                "Dubai AI Summit");
            tech1.AddTicketType("VIP Investor Pass",
                GetValue(Money.Create(3200, "USD"), "VIP Investor"), 3000, utcNow);
            tech1.AddTicketType("Full Conference Pass",
                GetValue(Money.Create(1500, "USD"), "Full Conference"), 15000, utcNow);
            tech1.AddTicketType("Expo Only Pass",
                GetValue(Money.Create(200, "USD"), "Expo"), 20000, utcNow);
            tech1.Publish(utcNow);
            events.Add(tech1);

            var addrTech2 = GetValue(
                Address.Create("Germany", "Berlin", "Berlin ExpoCenter City", "14055"),
                "address for Berlin TechWeek");
            var tech2 = GetValue(
                Event.Create("Berlin TechWeek 2027", 30000,
                    new DateTime(2027, 9, 6, 9, 0, 0),
                    addrTech2, "Europe's cutting-edge tech festival showcasing deep tech, SaaS, and green innovation.",
                    EventType.Tech, utcNow),
                "Berlin TechWeek");
            tech2.AddTicketType("Founder Pass",
                GetValue(Money.Create(1800, "USD"), "Founder"), 5000, utcNow);
            tech2.AddTicketType("Developer Pass",
                GetValue(Money.Create(600, "USD"), "Developer"), 12000, utcNow);
            tech2.AddTicketType("Student Pass",
                GetValue(Money.Create(150, "USD"), "Student"), 8000, utcNow);
            tech2.Publish(utcNow);
            events.Add(tech2);

            // ─── Sports ─────────────────────────────────────────────
            var addrSport1 = GetValue(
                Address.Create("Brazil", "Rio de Janeiro", "Maracanã Stadium", "20271-130"),
                "address for Rio Marathon");
            var sport1 = GetValue(
                Event.Create("Rio de Janeiro Marathon & Half-Marathon 2027", 35000,
                    new DateTime(2027, 8, 1, 6, 0, 0),
                    addrSport1, "Run along the iconic beaches of Copacabana and Ipanema in one of South America's biggest sporting events.",
                    EventType.Sports, utcNow),
                "Rio Marathon 2027");
            AddVenueTicketTypes(sport1, SportZones, "Sport", utcNow);
            sport1.Publish(utcNow);
            events.Add(sport1);

            var addrSport2 = GetValue(
                Address.Create("United States", "New York", "MetLife Stadium", "10001"),
                "address for FIFA World Cup Final");
            var sport2 = GetValue(
                Event.Create("FIFA World Cup 2027: Brazil vs Argentina — Final", 82500,
                    new DateTime(2027, 7, 18, 18, 0, 0),
                    addrSport2, "The grand finale of the FIFA World Cup 2027. Two South American giants battle for football's ultimate prize at MetLife Stadium.",
                    EventType.Sports, utcNow),
                "World Cup Final 2027");
            AddVenueTicketTypes(sport2, SportZones, "Sport", utcNow);
            sport2.Publish(utcNow);
            events.Add(sport2);

            var addrSport3 = GetValue(
                Address.Create("United States", "Los Angeles", "Rose Bowl Stadium", "91103"),
                "address for World Cup Semi-Final 1");
            var sport3 = GetValue(
                Event.Create("FIFA World Cup 2027: Semi-Final 1 — Germany vs France", 72000,
                    new DateTime(2027, 7, 14, 20, 0, 0),
                    addrSport3, "A European classic in the semi-final: Germany take on France at the iconic Rose Bowl with a place in the final on the line.",
                    EventType.Sports, utcNow),
                "World Cup Semi-Final 1");
            AddVenueTicketTypes(sport3, SportZones, "Sport", utcNow);
            sport3.Publish(utcNow);
            events.Add(sport3);

            var addrSport4 = GetValue(
                Address.Create("Mexico", "Mexico City", "Estadio Azteca", "01000"),
                "address for World Cup Semi-Final 2");
            var sport4 = GetValue(
                Event.Create("FIFA World Cup 2027: Semi-Final 2 — Portugal vs England", 85000,
                    new DateTime(2027, 7, 15, 15, 0, 0),
                    addrSport4, "The second semi-final at the legendary Estadio Azteca. Portugal's golden generation faces England's rising stars.",
                    EventType.Sports, utcNow),
                "World Cup Semi-Final 2");
            AddVenueTicketTypes(sport4, SportZones, "Sport", utcNow);
            sport4.Publish(utcNow);
            events.Add(sport4);

            var addrSport5 = GetValue(
                Address.Create("United States", "Atlanta", "Mercedes-Benz Stadium", "30313"),
                "address for World Cup Quarter-Final 1");
            var sport5 = GetValue(
                Event.Create("FIFA World Cup 2027: Quarter-Final — Italy vs Spain", 65000,
                    new DateTime(2027, 7, 10, 17, 0, 0),
                    addrSport5, "A Mediterranean derby in the quarter-finals. Italy vs Spain at Mercedes-Benz Stadium for a place in the semi-finals.",
                    EventType.Sports, utcNow),
                "World Cup Quarter-Final Italy vs Spain");
            AddVenueTicketTypes(sport5, SportZones, "Sport", utcNow);
            sport5.Publish(utcNow);
            events.Add(sport5);

            var addrSport6 = GetValue(
                Address.Create("United States", "Dallas", "AT&T Stadium", "76111"),
                "address for World Cup Quarter-Final 2");
            var sport6 = GetValue(
                Event.Create("FIFA World Cup 2027: Quarter-Final — Netherlands vs Belgium", 70000,
                    new DateTime(2027, 7, 10, 20, 0, 0),
                    addrSport6, "A Low Countries showdown in Dallas. Netherlands vs Belgium — bragging rights and a semi-final spot at stake.",
                    EventType.Sports, utcNow),
                "World Cup Quarter-Final Netherlands vs Belgium");
            AddVenueTicketTypes(sport6, SportZones, "Sport", utcNow);
            sport6.Publish(utcNow);
            events.Add(sport6);

            var addrSport7 = GetValue(
                Address.Create("United States", "Miami", "Hard Rock Stadium", "33056"),
                "address for World Cup Round of 16");
            var sport7 = GetValue(
                Event.Create("FIFA World Cup 2027: Round of 16 — Uruguay vs Croatia", 55000,
                    new DateTime(2027, 7, 6, 16, 0, 0),
                    addrSport7, "A knockout clash between two nations who've punched above their weight. Uruguay vs Croatia at Hard Rock Stadium.",
                    EventType.Sports, utcNow),
                "World Cup Round of 16 Uruguay vs Croatia");
            AddVenueTicketTypes(sport7, SportZones, "Sport", utcNow);
            sport7.Publish(utcNow);
            events.Add(sport7);

            // ─── Art ────────────────────────────────────────────────
            var addrArt1 = GetValue(
                Address.Create("France", "Paris", "Musée du Louvre", "75001"),
                "address for Louvre Exhibition");
            var art1 = GetValue(
                Event.Create("Louvre: Masters of Modern Art Exhibition 2027", 25000,
                    new DateTime(2027, 4, 10, 9, 0, 0),
                    addrArt1, "An exclusive exhibition featuring masterpieces from the Musée d'Orsay and the Louvre's modern art collection.",
                    EventType.Art, utcNow),
                "Louvre Masters Exhibition");
            art1.AddTicketType("VIP Guided Tour",
                GetValue(Money.Create(500, "USD"), "VIP Tour"), 500, utcNow);
            art1.AddTicketType("Full Access Pass",
                GetValue(Money.Create(120, "USD"), "Full Access"), 10000, utcNow);
            art1.AddTicketType("Standard Entry",
                GetValue(Money.Create(40, "USD"), "Standard"), 12000, utcNow);
            art1.Publish(utcNow);
            events.Add(art1);

            var addrArt2 = GetValue(
                Address.Create("Japan", "Tokyo", "Mori Arts Center Gallery", "106-6150"),
                "address for Tokyo Art Fair");
            var art2 = GetValue(
                Event.Create("Tokyo International Art Fair 2027", 20000,
                    new DateTime(2027, 11, 5, 10, 0, 0),
                    addrArt2, "Contemporary art from Asia's most innovative creators at the Roppongi Hills Mori Tower.",
                    EventType.Art, utcNow),
                "Tokyo Art Fair 2027");
            art2.AddTicketType("Collector's Pass",
                GetValue(Money.Create(450, "USD"), "Collector"), 2000, utcNow);
            art2.AddTicketType("General Admission",
                GetValue(Money.Create(60, "USD"), "GA"), 10000, utcNow);
            art2.Publish(utcNow);
            events.Add(art2);

            // ─── Food ───────────────────────────────────────────────
            var addrFood1 = GetValue(
                Address.Create("Egypt", "Cairo", "Zamalek Corniche", "11511"),
                "address for Cairo Food Festival");
            var food1 = GetValue(
                Event.Create("Cairo Nile Food Festival 2027", 30000,
                    new DateTime(2027, 5, 15, 16, 0, 0),
                    addrFood1, "Egyptian street food, international gourmet dishes, and live cooking competitions along the Nile.",
                    EventType.Food, utcNow),
                "Cairo Food Festival");
            food1.AddTicketType("VIP Tasting Pass",
                GetValue(Money.Create(200, "USD"), "VIP Tasting"), 3000, utcNow);
            food1.AddTicketType("Standard Entry",
                GetValue(Money.Create(30, "USD"), "Standard"), 20000, utcNow);
            food1.Publish(utcNow);
            events.Add(food1);

            var addrFood2 = GetValue(
                Address.Create("Japan", "Osaka", "Osaka Castle Park", "540-0002"),
                "address for Osaka Food Fest");
            var food2 = GetValue(
                Event.Create("Osaka Street Food & Ramen Festival 2027", 40000,
                    new DateTime(2027, 3, 22, 11, 0, 0),
                    addrFood2, "Japan's culinary capital presents its annual celebration of takoyaki, okonomiyaki, ramen, and sake.",
                    EventType.Food, utcNow),
                "Osaka Food Festival");
            food2.AddTicketType("Premium All-You-Can-Eat",
                GetValue(Money.Create(180, "USD"), "Premium"), 5000, utcNow);
            food2.AddTicketType("Tasting Pass (10 dishes)",
                GetValue(Money.Create(80, "USD"), "Tasting Pass"), 15000, utcNow);
            food2.AddTicketType("General Entry",
                GetValue(Money.Create(15, "USD"), "General"), 20000, utcNow);
            food2.Publish(utcNow);
            events.Add(food2);

            // ─── Education ──────────────────────────────────────────
            var addrEdu1 = GetValue(
                Address.Create("Egypt", "Alexandria", "Bibliotheca Alexandrina", "21526"),
                "address for Alexandria Knowledge Summit");
            var edu1 = GetValue(
                Event.Create("Alexandria Knowledge Summit 2027", 8000,
                    new DateTime(2027, 9, 20, 9, 0, 0),
                    addrEdu1, "A gathering of scholars, scientists, and thought leaders at the legendary Library of Alexandria.",
                    EventType.Education, utcNow),
                "Alexandria Knowledge Summit");
            edu1.AddTicketType("Academic Pass",
                GetValue(Money.Create(300, "USD"), "Academic"), 3000, utcNow);
            edu1.AddTicketType("Student Pass",
                GetValue(Money.Create(50, "USD"), "Student"), 4000, utcNow);
            edu1.Publish(utcNow);
            events.Add(edu1);

            var addrEdu2 = GetValue(
                Address.Create("France", "Lyon", "Centre de Congrès de Lyon", "69006"),
                "address for Lyon Education Forum");
            var edu2 = GetValue(
                Event.Create("Global Education Leaders Forum Lyon 2027", 5000,
                    new DateTime(2027, 6, 14, 8, 0, 0),
                    addrEdu2, "Ministers, educators, and EdTech founders reimagining the future of learning.",
                    EventType.Education, utcNow),
                "Lyon Education Forum");
            edu2.AddTicketType("Delegate Pass",
                GetValue(Money.Create(800, "USD"), "Delegate"), 1500, utcNow);
            edu2.AddTicketType("Educator Pass",
                GetValue(Money.Create(250, "USD"), "Educator"), 2500, utcNow);
            edu2.AddTicketType("Virtual Pass",
                GetValue(Money.Create(100, "USD"), "Virtual"), 5000, utcNow);
            edu2.Publish(utcNow);
            events.Add(edu2);

            // ─── Theater ────────────────────────────────────────────
            var addrTheatre1 = GetValue(
                Address.Create("Egypt", "Cairo", "Cairo Opera House", "11511"),
                "address for Cairo Opera Season");
            var theatre1 = GetValue(
                Event.Create("Cairo Opera: Aida — Verdi's Masterpiece", 1200,
                    new DateTime(2027, 4, 25, 20, 0, 0),
                    addrTheatre1, "Verdi's timeless opera performed by the Cairo Symphony Orchestra at the stunning Cairo Opera House.",
                    EventType.Theater, utcNow),
                "Cairo Opera Aida");
            AddVenueTicketTypes(theatre1, TheaterZones, "Theater", utcNow);
            theatre1.Publish(utcNow);
            events.Add(theatre1);

            var addrTheatre2 = GetValue(
                Address.Create("France", "Paris", "Opéra Garnier", "75009"),
                "address for Paris Ballet");
            var theatre2 = GetValue(
                Event.Create("Ballet de l'Opéra National de Paris — Swan Lake", 1800,
                    new DateTime(2027, 5, 8, 20, 0, 0),
                    addrTheatre2, "Tchaikovsky's Swan Lake performed by the world-famous Paris Opera Ballet at the Palais Garnier.",
                    EventType.Theater, utcNow),
                "Paris Opera Ballet");
            AddVenueTicketTypes(theatre2, TheaterZones, "Theater", utcNow);
            theatre2.Publish(utcNow);
            events.Add(theatre2);

            // ─── Outdoors ───────────────────────────────────────────
            var addrOut1 = GetValue(
                Address.Create("Egypt", "Giza", "Giza Plateau", "12511"),
                "address for Giza Desert Expedition");
            var out1 = GetValue(
                Event.Create("Giza Desert Safari & Pyramids Camping", 200,
                    new DateTime(2027, 10, 15, 5, 0, 0),
                    addrOut1, "A 3-day guided desert expedition including camel trekking, stargazing, and overnight camping with views of the Great Pyramids.",
                    EventType.Outdoors, utcNow),
                "Giza Desert Safari");
            out1.AddTicketType("Premium Camping Package",
                GetValue(Money.Create(600, "USD"), "Premium Package"), 50, utcNow);
            out1.AddTicketType("Standard Camping",
                GetValue(Money.Create(250, "USD"), "Standard"), 80, utcNow);
            out1.AddTicketType("Day Trip Only",
                GetValue(Money.Create(80, "USD"), "Day Trip"), 70, utcNow);
            out1.Publish(utcNow);
            events.Add(out1);

            var addrOut2 = GetValue(
                Address.Create("Australia", "Sydney", "Royal National Park Entrance", "2232"),
                "address for Sydney Coastal Trek");
            var out2 = GetValue(
                Event.Create("Royal National Park Coastal Trek — Sydney", 150,
                    new DateTime(2027, 3, 28, 6, 0, 0),
                    addrOut2, "A two-day guided hike along the stunning coastal cliffs, rainforest trails, and hidden beaches of Australia's oldest national park.",
                    EventType.Outdoors, utcNow),
                "Sydney Coastal Trek");
            out2.AddTicketType("Guided Trek + Camping Gear",
                GetValue(Money.Create(450, "USD"), "Full Package"), 40, utcNow);
            out2.AddTicketType("Guided Trek Only",
                GetValue(Money.Create(200, "USD"), "Trek Only"), 60, utcNow);
            out2.AddTicketType("Self-Guided Permit",
                GetValue(Money.Create(35, "USD"), "Permit"), 50, utcNow);
            out2.Publish(utcNow);
            events.Add(out2);

            return events;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating events: {Message}", ex.Message);
            throw;
        }
    }

    private static List<Booking> CreateSeedBookings(
        List<Event> events,
        List<UserId> attendeeIds,
        ILogger logger)
    {
        var bookings = new List<Booking>();
        var random = new Random();
        var utcNow = TimeProvider.System.GetUtcNow().UtcDateTime;

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
                    PaymentMethod.Instant, utcNow);

                if (bookingResult.IsFailure)
                {
                    logger.LogWarning("Failed to create booking: {Errors}",
                        string.Join(", ", bookingResult.Errors.Select(e => e.Message)));
                    continue;
                }

                var booking = bookingResult.Value;

                if (random.NextDouble() > 0.3)
                    booking.Confirm(utcNow);

                if (booking.Status == BookingStatusEnum.Confirmed && random.NextDouble() < 0.1)
                    booking.Cancel(utcNow, "Changed my mind");

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
