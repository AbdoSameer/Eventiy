using Application.Abstractions;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Common;
using Domain.Errors;

namespace Infrastructure.Services;

public sealed class VenueLayoutValidator : IVenueLayoutValidator
{
    private static readonly Dictionary<EventType, string[]> Layouts = new()
    {
        [EventType.Sports] = ["116", "124L", "234", "S105", "S118", "S180", "C129", "GC19", "VVIP1", "VVIP2"],
        [EventType.Music] = ["FP1", "FP2", "MF1", "MF2", "SB1", "SB2", "REAR", "VIP1", "VIP2"],
        [EventType.Theater] = ["ORCH", "ORCHL", "ORCHR", "MEZZ", "BALC", "BOXL", "BOXR", "FRONT"],
    };

    public bool HasVenueLayout(EventType eventType) => Layouts.ContainsKey(eventType);

    public string[] GetValidSections(EventType eventType) =>
        Layouts.TryGetValue(eventType, out var sections) ? sections : [];

    public Result ValidateSectionCode(EventType eventType, string? sectionCode)
    {
        if (string.IsNullOrWhiteSpace(sectionCode))
            return Result.Success();

        if (!HasVenueLayout(eventType))
            return Result.Success();

        var valid = Layouts[eventType];
        if (!valid.Contains(sectionCode))
            return Result.Failure(EventErrors.InvalidSectionCode(sectionCode, eventType));

        return Result.Success();
    }
}
