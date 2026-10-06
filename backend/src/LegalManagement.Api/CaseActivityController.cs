using System.ComponentModel.DataAnnotations;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

public record FollowUpRequest(Guid CaseStatusId, Guid CompetenceId, Guid CompetenceDetailId,
    [Required, StringLength(4000)] string Description, DateTime OccurredAt);
public record TaskRequest([Required, StringLength(240)] string Title, [StringLength(4000)] string? Description,
    Guid? AssignedMembershipId, string Priority = "MEDIUM", string Status = "PENDING", DateTime? DueAt = null);
public record TaskStatusRequest(string Status);

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/cases/{caseId:guid}")]
public class CaseActivityController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private async Task OwnCase(Guid caseId)
    {
        if (!await db.Cases.AnyAsync(x => x.Id == caseId && x.OrganizationId == tenant.OrganizationId))
            throw new ApiException(404, "Caso no encontrado.");
    }
    private Task<Guid> Creator() => db.Memberships.Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.Status == "ACTIVE").Select(x => x.Id).SingleAsync();
    private IQueryable<CaseFollowUp> FollowUps(Guid caseId) => db.CaseFollowUps.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.CaseId == caseId);
    private IQueryable<CaseTask> Tasks(Guid caseId) => db.CaseTasks.Where(x => x.OrganizationId == tenant.OrganizationId && x.CaseId == caseId);
    private IQueryable<object> FollowUpDtos(IQueryable<CaseFollowUp> query) => query.Select(x => new {
        x.Id, x.CaseId, x.CaseStatusId, x.CompetenceId, x.CompetenceDetailId, x.OccurredAt,
        Status = x.StatusName, Competence = x.CompetenceName, CompetenceDetail = x.CompetenceDetailName, x.Description,
        x.CreatedByMembershipId, CreatedBy = db.Users.Where(u => u.Id == x.CreatedByMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault(), x.CreatedAt, x.UpdatedAt });
    private IQueryable<object> TaskDtos(IQueryable<CaseTask> query) => query.Select(x => new {
        x.Id, x.CaseId, x.Title, x.Description, x.AssignedMembershipId,
        AssignedName = x.AssignedMembership == null ? null : db.Users.Where(u => u.Id == x.AssignedMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault(),
        x.Priority, x.Status, x.DueAt, x.CompletedAt, x.CreatedByMembershipId, x.CreatedAt, x.UpdatedAt });

    [HttpGet("follow-ups")]
    public async Task<object> ListFollowUps(Guid caseId)
    { await OwnCase(caseId); return await FollowUpDtos(FollowUps(caseId).OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)).ToListAsync(); }

    [HttpGet("follow-ups/{id:guid}")]
    public async Task<object> GetFollowUp(Guid caseId, Guid id)
    { await OwnCase(caseId); return await FollowUpDtos(FollowUps(caseId).Where(x => x.Id == id)).SingleOrDefaultAsync() ?? throw new ApiException(404, "Seguimiento no encontrado."); }

    [HttpPost("follow-ups")]
    public async Task<IActionResult> CreateFollowUp(Guid caseId, FollowUpRequest request)
    {
        await OwnCase(caseId);
        var status = await db.CaseStatuses.SingleOrDefaultAsync(x => x.Id == request.CaseStatusId && x.OrganizationId == tenant.OrganizationId && x.IsActive) ?? throw new ApiException(400, "Estado inválido o inactivo.");
        var general = await db.LegalCompetences.SingleOrDefaultAsync(x => x.Id == request.CompetenceId && x.OrganizationId == tenant.OrganizationId && x.IsActive && x.ParentId == null) ?? throw new ApiException(400, "Competencia general inválida o inactiva.");
        var detail = await db.LegalCompetences.SingleOrDefaultAsync(x => x.Id == request.CompetenceDetailId && x.OrganizationId == tenant.OrganizationId && x.IsActive && x.ParentId == general.Id) ?? throw new ApiException(400, "El detalle debe ser hijo activo de la competencia.");
        if (request.OccurredAt == default || string.IsNullOrWhiteSpace(request.Description)) throw new ApiException(400, "Fecha y descripción son obligatorias.");
        var entity = new CaseFollowUp { OrganizationId = tenant.OrganizationId, CaseId = caseId, CaseStatusId = status.Id,
            CompetenceId = general.Id, CompetenceDetailId = detail.Id, StatusName = status.Name, CompetenceName = general.Name,
            CompetenceDetailName = detail.Name, Description = request.Description.Trim(), OccurredAt = Utc(request.OccurredAt), CreatedByMembershipId = await Creator() };
        db.CaseFollowUps.Add(entity); await db.SaveChangesAsync();
        return Created($"/api/cases/{caseId}/follow-ups/{entity.Id}", await GetFollowUp(caseId, entity.Id));
    }

    [HttpGet("tasks")]
    public async Task<object> ListTasks(Guid caseId, [FromQuery] string? status, [FromQuery] string? priority,
        [FromQuery] Guid? assignedMembershipId, [FromQuery] DateTime? dueFrom, [FromQuery] DateTime? dueTo)
    {
        await OwnCase(caseId); var query = Tasks(caseId).AsNoTracking();
        if (status != null) { var value = Normalize(status, TaskStatuses.All); query = query.Where(x => x.Status == value); }
        if (priority != null) { var value = Normalize(priority, CasePriorities.All); query = query.Where(x => x.Priority == value); }
        if (assignedMembershipId.HasValue) query = query.Where(x => x.AssignedMembershipId == assignedMembershipId);
        if (dueFrom > dueTo) throw new ApiException(400, "Rango de vencimiento inválido.");
        if (dueFrom.HasValue) query = query.Where(x => x.DueAt >= dueFrom);
        if (dueTo.HasValue) query = query.Where(x => x.DueAt <= dueTo);
        return await TaskDtos(query.OrderBy(x => x.DueAt == null).ThenBy(x => x.DueAt).ThenByDescending(x => x.CreatedAt)).ToListAsync();
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask(Guid caseId, TaskRequest request)
    {
        await OwnCase(caseId); var entity = new CaseTask { OrganizationId = tenant.OrganizationId, CaseId = caseId, CreatedByMembershipId = await Creator() };
        await Apply(entity, request); db.CaseTasks.Add(entity); await db.SaveChangesAsync();
        return StatusCode(201, await TaskDtos(Tasks(caseId).Where(x => x.Id == entity.Id)).SingleAsync());
    }
    [HttpPut("tasks/{id:guid}")]
    public async Task<object> UpdateTask(Guid caseId, Guid id, TaskRequest request)
    { await OwnCase(caseId); var entity = await OwnTask(caseId, id); await Apply(entity, request); await db.SaveChangesAsync(); return await TaskDtos(Tasks(caseId).Where(x => x.Id == id)).SingleAsync(); }
    [HttpPatch("tasks/{id:guid}/status")]
    public async Task<object> ChangeStatus(Guid caseId, Guid id, TaskStatusRequest request)
    { await OwnCase(caseId); var entity = await OwnTask(caseId, id); SetStatus(entity, request.Status); await db.SaveChangesAsync(); return await TaskDtos(Tasks(caseId).Where(x => x.Id == id)).SingleAsync(); }
    private async Task<CaseTask> OwnTask(Guid caseId, Guid id) => await Tasks(caseId).SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Tarea no encontrada.");
    private async Task Apply(CaseTask entity, TaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ApiException(400, "El título es obligatorio.");
        if (request.AssignedMembershipId.HasValue && !await db.Memberships.AnyAsync(x => x.Id == request.AssignedMembershipId && x.OrganizationId == tenant.OrganizationId && x.Status == "ACTIVE")) throw new ApiException(400, "Responsable inválido o inactivo.");
        entity.Title = request.Title.Trim(); entity.Description = request.Description?.Trim(); entity.AssignedMembershipId = request.AssignedMembershipId;
        entity.Priority = Normalize(request.Priority, CasePriorities.All); entity.DueAt = request.DueAt.HasValue ? Utc(request.DueAt.Value) : null; SetStatus(entity, request.Status);
    }
    private static void SetStatus(CaseTask entity, string status)
    { entity.Status = Normalize(status, TaskStatuses.All); entity.CompletedAt = entity.Status == "COMPLETED" ? entity.CompletedAt ?? DateTime.UtcNow : null; entity.UpdatedAt = DateTime.UtcNow; }
    private static string Normalize(string? value, string[] allowed)
    { var result = value?.Trim().ToUpperInvariant(); return result != null && allowed.Contains(result) ? result : throw new ApiException(400, "Estado o prioridad inválidos."); }
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
