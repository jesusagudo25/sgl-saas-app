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
        [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] Guid? responsibleMembershipId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ApiException(400, "La paginacion no es valida.");
        var normalizedStatus = NormalizeOptional(status, CaseStatuses.All, "Estado de caso invalido.");
        var normalizedPriority = NormalizeOptional(priority, CasePriorities.All, "Prioridad de caso invalida.");
        var query = db.Cases.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        if (responsibleMembershipId.HasValue) query = query.Where(x => x.ResponsibleMembershipId == responsibleMembershipId);
        if (normalizedStatus is not null) query = query.Where(x => x.Status == normalizedStatus);
        if (normalizedPriority is not null) query = query.Where(x => x.Priority == normalizedPriority);
        if (!string.IsNullOrWhiteSpace(search)) {
            var value = search.Trim();
            query = query.Where(x => x.CaseNumber.Contains(value) || x.Title.Contains(value) ||
                x.Client.DisplayName.Contains(value) || (x.Counterparty != null && x.Counterparty.Contains(value)) ||
                (x.Court != null && x.Court.Contains(value)));
        }
        var total = await query.CountAsync();
        var items = await Project(query.OrderByDescending(x => x.OpenedAt).ThenBy(x => x.CaseNumber))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<CaseDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    [HttpGet("options")]
    public async Task<ActionResult<CaseOptionsDto>> Options()
    {
        var clients = await db.Clients.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.Status == ClientStatuses.Active)
            .OrderBy(x => x.DisplayName).Select(x => new CaseOptionDto(x.Id, x.DisplayName)).ToListAsync();
        var members = await (from m in db.Memberships.AsNoTracking() join u in db.Users on m.UserId equals u.Id
            where m.OrganizationId == tenant.OrganizationId && m.Status == "ACTIVE"
            orderby u.FirstName, u.LastName select new CaseOptionDto(m.Id, u.FirstName + " " + u.LastName)).ToListAsync();
        return new CaseOptionsDto(clients, members);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CaseDto>> Get(Guid id)
    {
        var item = await Project(db.Cases.AsNoTracking().Where(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)).SingleOrDefaultAsync();
        return item is null ? NotFound() : item;
    }

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CaseRequest request)
    {
        await ValidateRelations(request.ClientId, request.ResponsibleMembershipId);
        var entity = new Case { OrganizationId = tenant.OrganizationId };
        Apply(entity, request);
        await EnsureUniqueNumber(entity.CaseNumber);
        db.Cases.Add(entity); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, await FindDto(entity.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CaseDto>> Update(Guid id, CaseRequest request)
    {
        var entity = await Own(id); await ValidateRelations(request.ClientId, request.ResponsibleMembershipId);
        Apply(entity, request); await EnsureUniqueNumber(entity.CaseNumber, id);
        await db.SaveChangesAsync(); return await FindDto(id);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<CaseDto>> ChangeStatus(Guid id, ChangeCaseStatusRequest request)
    {
        var status = Normalize(request.Status, CaseStatuses.All, "Estado de caso invalido.");
        var entity = await Own(id); entity.Status = status; entity.ClosedAt = status == CaseStatuses.Closed ? entity.ClosedAt ?? DateTime.UtcNow : null;
        entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return await FindDto(id);
    }

    private async Task ValidateRelations(Guid clientId, Guid? membershipId)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId && x.OrganizationId == tenant.OrganizationId))
            throw new ApiException(400, "El cliente no pertenece a la organizacion activa.");
        if (membershipId.HasValue && !await db.Memberships.AnyAsync(x => x.Id == membershipId && x.OrganizationId == tenant.OrganizationId && x.Status == "ACTIVE"))
            throw new ApiException(400, "El responsable no es una membresia activa de la organizacion.");
    }

    private async Task EnsureUniqueNumber(string number, Guid? excluding = null)
    {
        if (await db.Cases.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.CaseNumber == number && x.Id != excluding))
            throw new ApiException(409, "Ya existe un caso con este numero en la organizacion.");
    }

    private async Task<Case> Own(Guid id) => await db.Cases.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)
        ?? throw new ApiException(404, "Caso no encontrado.");
    private async Task<CaseDto> FindDto(Guid id) => await Project(db.Cases.AsNoTracking().Where(x => x.Id == id)).SingleAsync();

    private static void Apply(Case entity, CaseRequest request)
    {
        entity.CaseNumber = Required(request.CaseNumber, "El numero de caso es obligatorio.").ToUpperInvariant();
        entity.Title = Required(request.Title, "El titulo es obligatorio."); entity.ClientId = request.ClientId;
        entity.CaseType = Clean(request.CaseType) ?? "GENERAL"; entity.Description = Clean(request.Description);
        entity.Status = Normalize(request.Status, CaseStatuses.All, "Estado de caso invalido.");
        entity.Priority = Normalize(request.Priority, CasePriorities.All, "Prioridad de caso invalida.");
        entity.ResponsibleMembershipId = request.ResponsibleMembershipId;
        if (request.OpenedAt == default) throw new ApiException(400, "La fecha de apertura es obligatoria.");
        entity.OpenedAt = request.OpenedAt; entity.ClosedAt = entity.Status == CaseStatuses.Closed ? entity.ClosedAt ?? DateTime.UtcNow : null;
        entity.Court = Clean(request.Court); entity.Jurisdiction = Clean(request.Jurisdiction); entity.Counterparty = Clean(request.Counterparty);
        entity.OpposingCounsel = Clean(request.OpposingCounsel); entity.Notes = Clean(request.Notes); entity.UpdatedAt = DateTime.UtcNow;
    }

    private IQueryable<CaseDto> Project(IQueryable<Case> query) => query.Select(x => new CaseDto(x.Id, x.ClientId,
        x.Client.DisplayName, x.CaseNumber, x.Title, x.Description, x.CaseType, x.Status, x.Priority,
        x.ResponsibleMembershipId, x.ResponsibleMembership == null ? null :
            db.Users.Where(u => u.Id == x.ResponsibleMembership.UserId).Select(u => u.FirstName + " " + u.LastName).SingleOrDefault(),
        x.OpenedAt, x.ClosedAt, x.Court, x.Jurisdiction,
        x.Counterparty, x.OpposingCounsel, x.Notes, x.CreatedAt, x.UpdatedAt));
    private static string Required(string? value, string message) => Clean(value) ?? throw new ApiException(400, message);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string value, string[] allowed, string error)
    { var result = value.Trim().ToUpperInvariant(); return allowed.Contains(result) ? result : throw new ApiException(400, error); }
    private static string? NormalizeOptional(string? value, string[] allowed, string error) => string.IsNullOrWhiteSpace(value) ? null : Normalize(value, allowed, error);
}
