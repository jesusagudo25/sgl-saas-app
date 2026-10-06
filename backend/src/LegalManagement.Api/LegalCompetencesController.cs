using System.ComponentModel.DataAnnotations;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;
public record CompetenceRequest([Required, StringLength(240)] string Name, Guid? ParentId = null, int SortOrder = 0);

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false)]
public class LegalCompetencesController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private IQueryable<LegalCompetence> Own => db.LegalCompetences.Where(x => x.OrganizationId == tenant.OrganizationId);
    private static object Dto(LegalCompetence x) => new { x.Id, x.Name, x.ParentId, x.IsActive, x.SortOrder, x.CreatedAt, x.UpdatedAt };
    [HttpGet("api/cases/competences")]
    public async Task<object> Options([FromQuery] Guid? parentId)
    { return (await Own.AsNoTracking().Where(x => x.IsActive && x.ParentId == parentId && (x.ParentId == null || x.Parent!.IsActive)).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync()).Select(Dto); }
    [Authorize(Policy = "OrganizationAdmin"), HttpGet("api/settings/competences")]
    public async Task<object> List() => (await Own.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync()).Select(Dto);
    [Authorize(Policy = "OrganizationAdmin"), HttpPost("api/settings/competences")]
    public async Task<IActionResult> Create(CompetenceRequest request)
    { var entity = new LegalCompetence { OrganizationId = tenant.OrganizationId }; await Apply(entity, request); db.LegalCompetences.Add(entity); await db.SaveChangesAsync(); return Created($"/api/settings/competences/{entity.Id}", Dto(entity)); }
    [Authorize(Policy = "OrganizationAdmin"), HttpPut("api/settings/competences/{id:guid}")]
    public async Task<object> Update(Guid id, CompetenceRequest request)
    { var entity = await Find(id); await Apply(entity, request); await db.SaveChangesAsync(); return Dto(entity); }
    [Authorize(Policy = "OrganizationAdmin"), HttpPatch("api/settings/competences/{id:guid}/status")]
    public async Task<object> Status(Guid id, CatalogStatusRequest request)
    { var entity = await Find(id); entity.IsActive = request.IsActive; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Dto(entity); }
    private async Task<LegalCompetence> Find(Guid id) => await Own.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Competencia no encontrada.");
    private async Task Apply(LegalCompetence entity, CompetenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ApiException(400, "Nombre obligatorio.");
        if (request.ParentId.HasValue && (request.ParentId == entity.Id || !await Own.AnyAsync(x => x.Id == request.ParentId && x.ParentId == null && (x.IsActive || x.Id == entity.ParentId)))) throw new ApiException(400, "Selecciona una competencia general activa de la organización.");
        if (entity.ParentId != request.ParentId && (await Own.AnyAsync(x => x.ParentId == entity.Id) || await db.CaseFollowUps.AnyAsync(x => x.CompetenceId == entity.Id || x.CompetenceDetailId == entity.Id))) throw new ApiException(409, "No se puede cambiar la jerarquía de una competencia con hijos o historial.");
        var name = request.Name.Trim();
        if (await Own.AnyAsync(x => x.Id != entity.Id && x.ParentId == request.ParentId && x.Name == name)) throw new ApiException(409, "Ya existe ese nombre en este nivel.");
        entity.Name = name; entity.ParentId = request.ParentId; entity.SortOrder = request.SortOrder; entity.UpdatedAt = DateTime.UtcNow;
    }
}
