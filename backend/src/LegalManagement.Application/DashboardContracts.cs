namespace LegalManagement.Application;

public class DashboardOptions
{
    public int StaleCaseDays { get; set; } = 30;
    public int UpcomingDays { get; set; } = 7;
    public int ListLimit { get; set; } = 10;
}
public record DashboardTaskDto(Guid Id, Guid CaseId, string Title, DateTimeOffset? DueAt);
public record DashboardEventDto(Guid Id, Guid? CaseId, string Title, DateTimeOffset StartsAt, bool AllDay);
public record DashboardCaseDto(Guid Id, string CaseNumber, string Title, DateTimeOffset? LastFollowUpAt);
public record DashboardFollowUpDto(Guid Id, Guid CaseId, string Description, DateTimeOffset OccurredAt);
public record DashboardDto(int ActiveCasesCount, int OverdueTasksCount, int TodayTasksCount, int TodayEventsCount,
    IReadOnlyList<DashboardTaskDto> UpcomingTasks, IReadOnlyList<DashboardEventDto> UpcomingEvents,
    IReadOnlyList<DashboardCaseDto> StaleCases, IReadOnlyList<DashboardFollowUpDto> LatestFollowUps,
    DateTimeOffset GeneratedAt, int StaleCaseDays, int UpcomingDays);
