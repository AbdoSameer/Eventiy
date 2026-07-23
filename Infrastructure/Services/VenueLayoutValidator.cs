using Application.Abstractions;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Infrastructure.Services;

public sealed class VenueLayoutValidator : IVenueLayoutValidator
{
    public bool HasVenueLayout(Domain.Aggregates.EventAggregate.Enums.EventType eventType) =>
        VenueLayout.HasLayout(eventType);

    public string[] GetValidSections(Domain.Aggregates.EventAggregate.Enums.EventType eventType) =>
        VenueLayout.GetValidSections(eventType);

    public Result ValidateSectionCode(Domain.Aggregates.EventAggregate.Enums.EventType eventType, string? sectionCode)
    {
        if (string.IsNullOrWhiteSpace(sectionCode))
            return Result.Success();

        if (!HasVenueLayout(eventType))
            return Result.Success();

        if (!VenueLayout.IsValidSection(eventType, sectionCode))
            return Result.Failure(EventErrors.InvalidSectionCode(sectionCode, eventType));

        return Result.Success();
    }
}
