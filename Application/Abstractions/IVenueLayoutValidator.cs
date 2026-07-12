using Domain.Aggregates.EventAggregate.Enums;
using Domain.Common;

namespace Application.Abstractions;

public interface IVenueLayoutValidator
{
    bool HasVenueLayout(EventType eventType);
    string[] GetValidSections(EventType eventType);
    Result ValidateSectionCode(EventType eventType, string? sectionCode);
}
