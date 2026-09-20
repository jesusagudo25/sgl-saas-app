using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Authorize(Policy = "OrganizationAdmin"), TenantRequired(false), Route("api/settings")]
public class SettingsCatalogsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("case-statuses")]
    public async Task<IReadOnlyList<CaseStatusDto>> Statuses() => await db.CaseStatuses.AsNoTracking()
        .Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        .Select(x => new CaseStatusDto(x.Id, x.Name, x.Code, x.IsActive, x.IsOpen, x.IsClosed, x.IsInnocent, x.IsGuilty, x.SortOrder, x.CreatedAt, x.UpdatedAt)).ToListAsync();

    [HttpPost("case-statuses")]
    public async Task<ActionResult<CaseStatusDto>> CreateStatus(CaseStatusRequest request)
    {
        var entity = new CaseStatus { OrganizationId = tenant.OrganizationId };
        await ApplyStatus(entity, request); db.CaseStatuses.Add(entity); await db.SaveChangesAsync();
        return Created($"/api/settings/case-statuses/{entity.Id}", ToDto(entity));
    }

    [HttpPut("case-statuses/{id:guid}")]
    public async Task<CaseStatusDto> UpdateStatus(Guid id, CaseStatusRequest request)
    { var entity = await Own(db.CaseStatuses, id); await ApplyStatus(entity, request); await db.SaveChangesAsync(); return ToDto(entity); }

    [HttpPatch("case-statuses/{id:guid}/status")]
    public async Task<CaseStatusDto> ToggleStatus(Guid id, CatalogStatusRequest request)
    { var entity = await Own(db.CaseStatuses, id); entity.IsActive = request.IsActive; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return ToDto(entity); }

    [HttpGet("case-types")] public Task<IReadOnlyList<CatalogDto>> Types() => List(db.CaseTypes);
    [HttpPost("case-types")] public Task<ActionResult<CatalogDto>> CreateType(CatalogRequest r) => Create(db.CaseTypes, r, "case-types");
    [HttpPut("case-types/{id:guid}")] public Task<CatalogDto> UpdateType(Guid id, CatalogRequest r) => Update(db.CaseTypes, id, r);
    [HttpPatch("case-types/{id:guid}/status")] public Task<CatalogDto> ToggleType(Guid id, CatalogStatusRequest r) => Toggle(db.CaseTypes, id, r);

    [HttpGet("courts")] public Task<IReadOnlyList<CatalogDto>> Courts() => List(db.Courts);
    [HttpPost("courts")] public Task<ActionResult<CatalogDto>> CreateCourt(CatalogRequest r) => Create(db.Courts, r, "courts");
    [HttpPut("courts/{id:guid}")] public Task<CatalogDto> UpdateCourt(Guid id, CatalogRequest r) => Update(db.Courts, id, r);
    [HttpPatch("courts/{id:guid}/status")] public Task<CatalogDto> ToggleCourt(Guid id, CatalogStatusRequest r) => Toggle(db.Courts, id, r);

    [HttpGet("jurisdictions")] public Task<IReadOnlyList<CatalogDto>> Jurisdictions() => List(db.Jurisdictions);
    [HttpPost("jurisdictions")] public Task<ActionResult<CatalogDto>> CreateJurisdiction(CatalogRequest r) => Create(db.Jurisdictions, r, "jurisdictions");
    [HttpPut("jurisdictions/{id:guid}")] public Task<CatalogDto> UpdateJurisdiction(Guid id, CatalogRequest r) => Update(db.Jurisdictions, id, r);
    [HttpPatch("jurisdictions/{id:guid}/status")] public Task<CatalogDto> ToggleJurisdiction(Guid id, CatalogStatusRequest r) => Toggle(db.Jurisdictions, id, r);

    private async Task<IReadOnlyList<CatalogDto>> List<T>(DbSet<T> set) where T : OrganizationCatalog => await set.AsNoTracking()
        .Where(x => x.OrganizationId == tenant.OrganizationId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        .Select(x => new CatalogDto(x.Id, x.Name, x.IsActive, x.SortOrder, x.CreatedAt, x.UpdatedAt)).ToListAsync();

    private async Task<ActionResult<CatalogDto>> Create<T>(DbSet<T> set, CatalogRequest request, string route) where T : OrganizationCatalog, new()
    { var entity = new T { OrganizationId = tenant.OrganizationId }; await Apply(entity, request, set); set.Add(entity); await db.SaveChangesAsync(); return Created($"/api/settings/{route}/{entity.Id}", ToDto(entity)); }

    private async Task<CatalogDto> Update<T>(DbSet<T> set, Guid id, CatalogRequest request) where T : OrganizationCatalog
    { var entity = await Own(set, id); await Apply(entity, request, set); await db.SaveChangesAsync(); return ToDto(entity); }

    private async Task<CatalogDto> Toggle<T>(DbSet<T> set, Guid id, CatalogStatusRequest request) where T : OrganizationCatalog
    { var entity = await Own(set, id); entity.IsActive = request.IsActive; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return ToDto(entity); }

    private async Task Apply<T>(T entity, CatalogRequest request, DbSet<T> set) where T : OrganizationCatalog
    {
        var name = Required(request.Name);
        if (await set.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Name == name && x.Id != entity.Id)) throw new ApiException(409, "Ya existe un registro con ese nombre.");
        entity.Name = name; entity.SortOrder = request.SortOrder; entity.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ApplyStatus(CaseStatus entity, CaseStatusRequest request)
    {
        var name = Required(request.Name); var code = Required(request.Code).ToUpperInvariant().Replace(' ', '_');
        if (await db.CaseStatuses.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id != entity.Id && (x.Name == name || x.Code == code)))
            throw new ApiException(409, "Ya existe un estado con ese nombre o código.");
        entity.Name = name; entity.Code = code; entity.IsOpen = request.IsOpen; entity.IsClosed = request.IsClosed;
        entity.IsInnocent = request.IsInnocent; entity.IsGuilty = request.IsGuilty; entity.SortOrder = request.SortOrder; entity.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<T> Own<T>(DbSet<T> set, Guid id) where T : OrganizationCatalog => await set.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)
        ?? throw new ApiException(404, "Registro no encontrado.");
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ApiException(400, "El nombre es obligatorio.") : value.Trim();
    private static CatalogDto ToDto(OrganizationCatalog x) => new(x.Id, x.Name, x.IsActive, x.SortOrder, x.CreatedAt, x.UpdatedAt);
    private static CaseStatusDto ToDto(CaseStatus x) => new(x.Id, x.Name, x.Code, x.IsActive, x.IsOpen, x.IsClosed, x.IsInnocent, x.IsGuilty, x.SortOrder, x.CreatedAt, x.UpdatedAt);
}
