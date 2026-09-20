using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/cases")]
public class CasesController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CaseDto>>> List([FromQuery] string? search, [FromQuery] Guid? clientId,
        [FromQuery] Guid? caseStatusId, [FromQuery] string? status, [FromQuery] string? priority,
        [FromQuery] Guid? responsibleMembershipId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ApiException(400, "La paginación no es válida.");
        var normalizedPriority = NormalizeOptional(priority, CasePriorities.All, "Prioridad de caso inválida.");
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        var query = db.Cases.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        if (responsibleMembershipId.HasValue) query = query.Where(x => x.ResponsibleMembershipId == responsibleMembershipId);
        if (caseStatusId.HasValue) query = query.Where(x => x.CaseStatusId == caseStatusId);
        else if (normalizedStatus is not null) query = query.Where(x => x.Status == normalizedStatus || (x.CaseStatus != null && x.CaseStatus.Code == normalizedStatus));
        if (normalizedPriority is not null) query = query.Where(x => x.Priority == normalizedPriority);
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim(); query = query.Where(x => x.CaseNumber.Contains(value) || x.Title.Contains(value) || x.Client.DisplayName.Contains(value) || (x.Counterparty != null && x.Counterparty.Contains(value)) || (x.CourtReference != null && x.CourtReference.Name.Contains(value)) || (x.Court != null && x.Court.Contains(value))); }
        var total = await query.CountAsync(); var items = await Project(query.OrderByDescending(x => x.OpenedAt).ThenBy(x => x.CaseNumber)).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<CaseDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    [HttpGet("options")]
    public async Task<ActionResult<CaseOptionsDto>> Options()
    {
        var clients = await db.Clients.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.Status == ClientStatuses.Active).OrderBy(x => x.DisplayName).Select(x => new CaseOptionDto(x.Id, x.DisplayName)).ToListAsync();
        var members = await (from m in db.Memberships.AsNoTracking() join u in db.Users on m.UserId equals u.Id where m.OrganizationId == tenant.OrganizationId && m.Status == "ACTIVE" orderby u.FirstName, u.LastName select new CaseOptionDto(m.Id, u.FirstName + " " + u.LastName)).ToListAsync();
        var statuses = await db.CaseStatuses.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new CaseStatusOptionDto(x.Id, x.Name, x.Code, x.IsOpen, x.IsClosed, x.IsInnocent, x.IsGuilty)).ToListAsync();
        return new CaseOptionsDto(clients, members, statuses, await ActiveOptions(db.CaseTypes), await ActiveOptions(db.Courts), await ActiveOptions(db.Jurisdictions));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CaseDto>> Get(Guid id) { var item = await Project(db.Cases.AsNoTracking().Where(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)).SingleOrDefaultAsync(); return item is null ? NotFound() : item; }

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CaseRequest request)
    { var entity = new Case { OrganizationId = tenant.OrganizationId }; await ValidateAndApply(entity, request); await EnsureUniqueNumber(entity.CaseNumber); db.Cases.Add(entity); await db.SaveChangesAsync(); return CreatedAtAction(nameof(Get), new { id = entity.Id }, await FindDto(entity.Id)); }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CaseDto>> Update(Guid id, CaseRequest request)
    { var entity = await Own(id); await ValidateAndApply(entity, request); await EnsureUniqueNumber(entity.CaseNumber, id); await db.SaveChangesAsync(); return await FindDto(id); }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<CaseDto>> ChangeStatus(Guid id, ChangeCaseStatusRequest request)
    {
        var entity = await Own(id); var code = request.Status.Trim().ToUpperInvariant(); Guid.TryParse(request.Status, out var statusId);
        var catalog = await db.CaseStatuses.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.IsActive && (x.Code == code || x.Id == statusId));
        if (catalog is null && !CaseStatuses.All.Contains(code)) throw new ApiException(400, "Estado de caso inválido.");
        entity.CaseStatusId = catalog?.Id; entity.Status = catalog?.Code ?? code; var closes = catalog?.IsClosed ?? code == CaseStatuses.Closed;
        entity.ClosedAt = closes ? entity.ClosedAt ?? DateTime.UtcNow : null; entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return await FindDto(id);
    }

    private async Task ValidateAndApply(Case entity, CaseRequest request)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == request.ClientId && x.OrganizationId == tenant.OrganizationId)) throw new ApiException(400, "El cliente no pertenece a la organización activa.");
        if (request.ResponsibleMembershipId.HasValue && !await db.Memberships.AnyAsync(x => x.Id == request.ResponsibleMembershipId && x.OrganizationId == tenant.OrganizationId && x.Status == "ACTIVE")) throw new ApiException(400, "El responsable no es una membresía activa de la organización.");
        entity.CaseNumber = Required(request.CaseNumber, "El número de caso es obligatorio.").ToUpperInvariant(); entity.Title = Required(request.Title, "El título es obligatorio."); entity.ClientId = request.ClientId;
        entity.Description = Clean(request.Description); entity.Priority = Normalize(request.Priority, CasePriorities.All, "Prioridad de caso inválida."); entity.ResponsibleMembershipId = request.ResponsibleMembershipId;
        if (request.OpenedAt == default) throw new ApiException(400, "La fecha de apertura es obligatoria."); entity.OpenedAt = request.OpenedAt; entity.SituationDate = request.SituationDate;
        entity.CaseStatus = await ResolveStatus(request.CaseStatusId, request.Status); entity.CaseStatusId = entity.CaseStatus.Id; entity.Status = entity.CaseStatus.Code;
        entity.CaseTypeReference = await ResolveCatalog(db.CaseTypes, request.CaseTypeId, request.CaseType, "tipo de caso"); entity.CaseTypeId = entity.CaseTypeReference.Id; entity.CaseType = entity.CaseTypeReference.Name;
        entity.CourtReference = await ResolveOptionalCatalog(db.Courts, request.CourtId, request.Court, "tribunal"); entity.CourtId = entity.CourtReference?.Id; entity.Court = entity.CourtReference?.Name;
        entity.JurisdictionReference = await ResolveOptionalCatalog(db.Jurisdictions, request.JurisdictionId, request.Jurisdiction, "jurisdicción"); entity.JurisdictionId = entity.JurisdictionReference?.Id; entity.Jurisdiction = entity.JurisdictionReference?.Name;
        entity.ClosedAt = entity.CaseStatus.IsClosed ? entity.ClosedAt ?? DateTime.UtcNow : null; entity.Counterparty = Clean(request.Counterparty); entity.OpposingCounsel = Clean(request.OpposingCounsel); entity.Notes = Clean(request.Notes); entity.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<CaseStatus> ResolveStatus(Guid? id, string? legacy)
    {
        if (id.HasValue) return await db.CaseStatuses.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId && x.IsActive) ?? throw new ApiException(400, "El estado no pertenece a la organización activa o está inactivo.");
        var code = Normalize(legacy ?? "", CaseStatuses.All, "Estado de caso inválido."); var found = await db.CaseStatuses.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Code == code);
        if (found is not null) return found; found = new CaseStatus { OrganizationId = tenant.OrganizationId, Name = code.Replace('_', ' '), Code = code, IsOpen = code != CaseStatuses.Closed, IsClosed = code == CaseStatuses.Closed }; db.CaseStatuses.Add(found); return found;
    }
    private async Task<T> ResolveCatalog<T>(DbSet<T> set, Guid? id, string? legacy, string label) where T : OrganizationCatalog, new()
    {
        if (id.HasValue) return await set.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId && x.IsActive) ?? throw new ApiException(400, $"El {label} no pertenece a la organización activa o está inactivo.");
        var name = Clean(legacy) ?? "GENERAL"; var found = await set.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.Name == name); if (found is not null) return found;
        found = new T { OrganizationId = tenant.OrganizationId, Name = name }; set.Add(found); return found;
    }
    private async Task<T?> ResolveOptionalCatalog<T>(DbSet<T> set, Guid? id, string? legacy, string label) where T : OrganizationCatalog, new() => !id.HasValue && Clean(legacy) is null ? null : await ResolveCatalog(set, id, legacy, label);
    private async Task<List<CaseOptionDto>> ActiveOptions<T>(DbSet<T> set) where T : OrganizationCatalog => await set.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new CaseOptionDto(x.Id, x.Name)).ToListAsync();
    private async Task EnsureUniqueNumber(string number, Guid? excluding = null) { if (await db.Cases.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.CaseNumber == number && x.Id != excluding)) throw new ApiException(409, "Ya existe un caso con este número en la organización."); }
    private async Task<Case> Own(Guid id) => await db.Cases.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId) ?? throw new ApiException(404, "Caso no encontrado.");
    private async Task<CaseDto> FindDto(Guid id) => await Project(db.Cases.AsNoTracking().Where(x => x.Id == id)).SingleAsync();
    private IQueryable<CaseDto> Project(IQueryable<Case> q) => q.Select(x => new CaseDto(x.Id, x.ClientId, x.Client.DisplayName, x.CaseNumber, x.Title, x.Description,
        x.CaseTypeReference != null ? x.CaseTypeReference.Name : x.CaseType, x.CaseTypeId, x.CaseStatus != null ? x.CaseStatus.Code : x.Status, x.CaseStatusId, x.Priority, x.ResponsibleMembershipId,
        x.ResponsibleMembership == null ? null : db.Users.Where(u => u.Id == x.ResponsibleMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault(),
        x.OpenedAt, x.SituationDate, x.ClosedAt, x.CourtReference != null ? x.CourtReference.Name : x.Court, x.CourtId, x.JurisdictionReference != null ? x.JurisdictionReference.Name : x.Jurisdiction, x.JurisdictionId, x.Counterparty, x.OpposingCounsel, x.Notes, x.CreatedAt, x.UpdatedAt));
    private static string Required(string? value, string message) => Clean(value) ?? throw new ApiException(400, message);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string value, string[] allowed, string error) { var result = value.Trim().ToUpperInvariant(); return allowed.Contains(result) ? result : throw new ApiException(400, error); }
    private static string? NormalizeOptional(string? value, string[] allowed, string error) => string.IsNullOrWhiteSpace(value) ? null : Normalize(value, allowed, error);
}
