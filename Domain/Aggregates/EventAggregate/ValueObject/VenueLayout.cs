namespace Domain.Aggregates.EventAggregate.ValueObject;

public static class VenueLayout
{
    private static readonly Dictionary<Enums.EventType, string[]> Layouts = new()
    {
        [Enums.EventType.Sports] = ["116", "124L", "234", "S105", "S118", "S180", "C129", "GC19", "VVIP1", "VVIP2"],
        [Enums.EventType.Music] = ["FP1", "FP2", "MF1", "MF2", "SB1", "SB2", "REAR", "VIP1", "VIP2"],
        [Enums.EventType.Theater] = ["ORCH", "ORCHL", "ORCHR", "MEZZ", "BALC", "BOXL", "BOXR", "FRONT"],
    };

    public static bool HasLayout(Enums.EventType eventType) => Layouts.ContainsKey(eventType);

    public static string[] GetValidSections(Enums.EventType eventType) =>
        Layouts.TryGetValue(eventType, out var sections) ? sections : [];

    public static bool IsValidSection(Enums.EventType eventType, string sectionCode) =>
        Layouts.TryGetValue(eventType, out var sections) && sections.Contains(sectionCode);
}
