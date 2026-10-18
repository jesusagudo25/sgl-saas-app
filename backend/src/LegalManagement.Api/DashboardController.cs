using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LegalManagement.Api;

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/dashboard")]
public class DashboardController(AppDbContext db, ITenantContext tenant, IOptions<DashboardOptions> options, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<DashboardDto> Get([FromQuery] string? timeZone = null)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "America/Panama"); }
        catch (TimeZoneNotFoundException) { throw new ApiException(400, "Zona horaria inválida."); }
        catch (InvalidTimeZoneException) { throw new ApiException(400, "Zona horaria inválida."); }
        var generated = clock.GetUtcNow(); var now = generated.UtcDateTime;
        var today = TimeZoneInfo.ConvertTime(generated, zone).Date;
        var days = Math.Clamp(options.Value.UpcomingDays, 1, 31);
        var staleDays = Math.Clamp(options.Value.StaleCaseDays, 1, 3650);
        var limit = Math.Clamp(options.Value.ListLimit, 1, 50);
        var tomorrow = today.AddDays(1); var endDate = today.AddDays(days);
        var from = DayBoundary(today, zone);
        var to = DayBoundary(tomorrow, zone);
        var end = DayBoundary(endDate, zone);
        var cutoff = now.AddDays(-staleDays);
        var cases = db.Cases.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        // Legacy cases without a catalog reference keep the existing status-code semantics.
        var active = cases.Where(x => x.CaseStatus != null ? x.CaseStatus.IsOpen : x.Status == "OPEN" || x.Status == "IN_PROGRESS" || x.Status == "SUSPENDED");
        var tasks = db.CaseTasks.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.Status != "COMPLETED" && x.Status != "CANCELLED");
        var events = db.CalendarEvents.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.Status == "SCHEDULED");
        var followUps = db.CaseFollowUps.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        var activeCount = await active.CountAsync();
        var overdueCount = await tasks.CountAsync(x => x.DueAt < now);
        var todayCount = await tasks.CountAsync(x => x.DueAt >= from && x.DueAt < to);
        var eventCount = await events.CountAsync(x => x.AllDay ? x.StartsAt < tomorrow && x.EndsAt >= today : x.StartsAt < to && (x.EndsAt > from || x.StartsAt == x.EndsAt && x.StartsAt >= from));
        var upcomingTasks = await tasks.Where(x => x.DueAt >= now && x.DueAt < end).OrderBy(x => x.DueAt).ThenBy(x => x.Id).Take(limit)
            .Select(x => new DashboardTaskDto(x.Id, x.CaseId, x.Title, x.DueAt.HasValue ? Utc(x.DueAt.Value) : null)).ToListAsync();
        var upcomingEvents = await events.Where(x => x.AllDay ? x.StartsAt < endDate && x.EndsAt >= today : x.StartsAt < end && (x.EndsAt > now || x.StartsAt == x.EndsAt && x.StartsAt >= now))
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Take(limit).Select(x => new DashboardEventDto(x.Id, x.CaseId, x.Title, Utc(x.StartsAt), x.AllDay)).ToListAsync();
        var staleCases = await active.Select(x => new { x.Id, x.CaseNumber, x.Title, x.CreatedAt,
                Last = followUps.Where(f => f.CaseId == x.Id).Max(f => (DateTime?)f.OccurredAt) })
            .Where(x => (x.Last ?? x.CreatedAt) < cutoff).OrderBy(x => x.Last ?? x.CreatedAt).ThenBy(x => x.Id).Take(limit)
            .Select(x => new DashboardCaseDto(x.Id, x.CaseNumber, x.Title, x.Last.HasValue ? Utc(x.Last.Value) : null)).ToListAsync();
        var latest = await followUps.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(limit)
            .Select(x => new DashboardFollowUpDto(x.Id, x.CaseId, x.Description, Utc(x.OccurredAt))).ToListAsync();
        return new(activeCount, overdueCount, todayCount, eventCount, upcomingTasks, upcomingEvents, staleCases, latest, generated, staleDays, days);
    }
    // Civil days may start with a skipped or repeated midnight in some browser zones.
    private static DateTime DayBoundary(DateTime day, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(day, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return zone.IsAmbiguousTime(local)
            ? new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime
            : TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
