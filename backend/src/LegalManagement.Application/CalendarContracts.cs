using System.ComponentModel.DataAnnotations;

namespace LegalManagement.Application;

public record CalendarEventRequest(
    [Required, StringLength(240)] string Title,
    Guid? CaseId,
    [Required] string EventType,
    string Status,
    Guid? AssignedMembershipId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool AllDay = false,
    [StringLength(500)] string? Location = null,
    [StringLength(2048)] string? MeetingUrl = null,
    [StringLength(4000)] string? Description = null);

public record CalendarEventStatusRequest(string Status);

public record CalendarEventDto(Guid Id, Guid? CaseId, string? CaseName, string Title, string? Description,
    string EventType, string Status, Guid? AssignedMembershipId, string? AssignedName,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool AllDay, string? Location, string? MeetingUrl,
    Guid CreatedByMembershipId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record CalendarOptionsDto(IReadOnlyList<CaseOptionDto> Cases, IReadOnlyList<CaseOptionDto> ResponsibleMemberships);
