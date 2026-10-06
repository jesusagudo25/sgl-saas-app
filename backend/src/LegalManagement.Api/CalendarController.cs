using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/calendar")]
public class CalendarController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private IQueryable<CalendarEvent> Own => db.CalendarEvents.Where(x => x.OrganizationId == tenant.OrganizationId);

    [HttpGet("options")]
    public async Task<CalendarOptionsDto> Options()
    {
        var cases = await db.Cases.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId)
            .OrderBy(x => x.CaseNumber).Select(x => new CaseOptionDto(x.Id, x.CaseNumber + " · " + x.Title)).ToListAsync();
        var members = await (from m in db.Memberships.AsNoTracking() join u in db.Users on m.UserId equals u.Id
            where m.OrganizationId == tenant.OrganizationId && m.Status == "ACTIVE"
            orderby u.FirstName, u.LastName select new CaseOptionDto(m.Id, u.FirstName + " " + u.LastName)).ToListAsync();
        return new(cases, members);
    }

    [HttpGet("events")]
    public async Task<IReadOnlyList<CalendarEventDto>> List([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? caseId, [FromQuery] Guid? assignedMembershipId, [FromQuery] string? eventType, [FromQuery] string? status)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value) throw new ApiException(400, "Rango de fechas invertido.");
        var query = Own.AsNoTracking();
        if (caseId.HasValue) { await ValidateCase(caseId.Value); query = query.Where(x => x.CaseId == caseId); }
        if (assignedMembershipId.HasValue) query = query.Where(x => x.AssignedMembershipId == assignedMembershipId);
        if (eventType != null) { var value = Normalize(eventType, CalendarEventTypes.All); query = query.Where(x => x.EventType == value); }
        if (status != null) { var value = Normalize(status, CalendarEventStatuses.All); query = query.Where(x => x.Status == value); }
        // Timed events overlap an instant range; all-day events overlap civil dates.
        // The upper bound is exclusive, matching the visible FullCalendar interval.
        if (from.HasValue) {
            var instant = from.Value.UtcDateTime; var day = from.Value.Date;
            query = query.Where(x => x.AllDay ? x.EndsAt >= day : x.EndsAt > instant || (x.StartsAt == x.EndsAt && x.StartsAt == instant));
        }
        if (to.HasValue) {
            var instant = to.Value.UtcDateTime; var day = to.Value.Date;
            if (to.Value.TimeOfDay != TimeSpan.Zero) day = day.AddDays(1);
            query = query.Where(x => x.AllDay ? x.StartsAt < day : x.StartsAt < instant);
        }
        return await Project(query.OrderBy(x => x.StartsAt).ThenBy(x => x.Id)).ToListAsync();
    }

    [HttpGet("events/{id:guid}")]
    public async Task<CalendarEventDto> Get(Guid id) => await Dto(id);

    [HttpPost("events")]
    public async Task<ActionResult<CalendarEventDto>> Create(CalendarEventRequest request)
    {
        var entity = new CalendarEvent { OrganizationId = tenant.OrganizationId,
            CreatedByMembershipId = await db.Memberships.Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.Status == "ACTIVE").Select(x => x.Id).SingleAsync() };
        await Apply(entity, request); db.CalendarEvents.Add(entity); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, await Dto(entity.Id));
    }

    [HttpPut("events/{id:guid}")]
    public async Task<CalendarEventDto> Update(Guid id, CalendarEventRequest request)
    { var entity = await Find(id); await Apply(entity, request); await db.SaveChangesAsync(); return await Dto(id); }

    [HttpPatch("events/{id:guid}/status")]
    public async Task<CalendarEventDto> Status(Guid id, CalendarEventStatusRequest request)
    { var entity = await Find(id); entity.Status = Normalize(request.Status, CalendarEventStatuses.All); entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return await Dto(id); }

    private async Task Apply(CalendarEvent entity, CalendarEventRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) throw new ApiException(400, "El título es obligatorio.");
        if (r.CaseId.HasValue) await ValidateCase(r.CaseId.Value);
        if (r.AssignedMembershipId.HasValue && !await db.Memberships.AnyAsync(x => x.Id == r.AssignedMembershipId && x.OrganizationId == tenant.OrganizationId && x.Status == "ACTIVE"))
            throw new ApiException(400, "El responsable debe ser una membresía activa de la organización.");
        if (r.StartsAt == default || r.EndsAt == default) throw new ApiException(400, "Inicio y fin son obligatorios.");
        if (r.EndsAt < r.StartsAt) throw new ApiException(400, "El fin debe ser igual o posterior al inicio.");
        var start = r.AllDay ? DateTime.SpecifyKind(r.StartsAt.Date, DateTimeKind.Utc) : r.StartsAt.UtcDateTime;
        var end = r.AllDay ? DateTime.SpecifyKind(r.EndsAt.Date, DateTimeKind.Utc) : r.EndsAt.UtcDateTime;
        if (end < start) throw new ApiException(400, "Las fechas de todo el día están invertidas.");
        var url = Clean(r.MeetingUrl);
        if (url != null && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != "https" && parsed.Scheme != "http")))
            throw new ApiException(400, "La URL de reunión debe comenzar con https:// o http://.");
        entity.Title = r.Title.Trim(); entity.CaseId = r.CaseId; entity.Description = Clean(r.Description);
        entity.EventType = Normalize(r.EventType, CalendarEventTypes.All); entity.Status = Normalize(r.Status, CalendarEventStatuses.All);
        entity.AssignedMembershipId = r.AssignedMembershipId; entity.StartsAt = start; entity.EndsAt = end; entity.AllDay = r.AllDay;
        entity.Location = Clean(r.Location); entity.MeetingUrl = url; entity.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ValidateCase(Guid id)
    { if (!await db.Cases.AnyAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)) throw new ApiException(400, "El caso debe pertenecer a la organización activa."); }
    private async Task<CalendarEvent> Find(Guid id) => await Own.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Evento no encontrado.");
    private async Task<CalendarEventDto> Dto(Guid id) => await Project(Own.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync() ?? throw new ApiException(404, "Evento no encontrado.");
    private IQueryable<CalendarEventDto> Project(IQueryable<CalendarEvent> query) => query.Select(x => new CalendarEventDto(x.Id, x.CaseId, x.Case == null ? null : x.Case.CaseNumber + " · " + x.Case.Title,
        x.Title, x.Description, x.EventType, x.Status, x.AssignedMembershipId,
        x.AssignedMembership == null ? null : db.Users.Where(u => u.Id == x.AssignedMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault(),
        Utc(x.StartsAt), Utc(x.EndsAt), x.AllDay, x.Location, x.MeetingUrl, x.CreatedByMembershipId, Utc(x.CreatedAt), Utc(x.UpdatedAt)));
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string? value, string[] allowed)
    { var result = value?.Trim().ToUpperInvariant(); return result != null && allowed.Contains(result) ? result : throw new ApiException(400, "Tipo o estado de evento inválido."); }
}
