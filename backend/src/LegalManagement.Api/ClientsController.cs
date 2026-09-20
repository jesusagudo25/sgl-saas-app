using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Authorize(Policy = "OrganizationMember"), TenantRequired(false), Route("api/clients")]
public class ClientsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientDto>>> List([FromQuery] string? search, [FromQuery] string? type,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ApiException(400, "La paginación no es válida.");
        if (type is not null && !ClientTypes.All.Contains(type.ToUpperInvariant())) throw new ApiException(400, "Tipo de cliente inválido.");
        if (status is not null && !ClientStatuses.All.Contains(status.ToUpperInvariant())) throw new ApiException(400, "Estado de cliente inválido.");
        var query = db.Clients.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId);
        if (!string.IsNullOrWhiteSpace(type)) { var value = type.Trim().ToUpperInvariant(); query = query.Where(x => x.Type == value); }
        if (!string.IsNullOrWhiteSpace(status)) { var value = status.Trim().ToUpperInvariant(); query = query.Where(x => x.Status == value); }
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim(); query = query.Where(x => x.DisplayName.Contains(value) ||
            x.IdentificationNumber.Contains(value) || (x.Email != null && x.Email.Contains(value)) ||
            (x.Phone != null && x.Phone.Contains(value)) || (x.TradeName != null && x.TradeName.Contains(value))); }
        var total = await query.CountAsync();
        var entities = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = entities.Select(ToDto).ToList();
        return new PagedResult<ClientDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> Get(Guid id)
    {
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId);
        return client is null ? NotFound() : ToDto(client);
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientRequest request)
    {
        var client = new Client { OrganizationId = tenant.OrganizationId };
        Apply(client, request);
        if (await db.Clients.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.IdentificationNumber == client.IdentificationNumber))
            throw new ApiException(409, "Ya existe un cliente con esta identificación en la organización.");
        db.Clients.Add(client); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = client.Id }, ToDto(client));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientDto>> Update(Guid id, ClientRequest request)
    {
        var client = await Own(id); Apply(client, request);
        if (await db.Clients.AnyAsync(x => x.OrganizationId == tenant.OrganizationId && x.Id != id && x.IdentificationNumber == client.IdentificationNumber))
            throw new ApiException(409, "Ya existe un cliente con esta identificación en la organización.");
        await db.SaveChangesAsync(); return ToDto(client);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ClientDto>> ChangeStatus(Guid id, ChangeClientStatusRequest request)
    {
        var value = request.Status.Trim().ToUpperInvariant();
        if (!ClientStatuses.All.Contains(value)) throw new ApiException(400, "Estado de cliente inválido.");
        var client = await Own(id); client.Status = value; client.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return ToDto(client);
    }

    private async Task<Client> Own(Guid id) => await db.Clients.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId)
        ?? throw new ApiException(404, "Cliente no encontrado.");
    private static void Apply(Client client, ClientRequest request)
    {
        var type = request.Type.Trim().ToUpperInvariant();
        if (!ClientTypes.All.Contains(type)) throw new ApiException(400, "Tipo de cliente inválido.");
        var firstName = Clean(request.FirstName); var lastName = Clean(request.LastName);
        var legalName = Clean(request.LegalName); var tradeName = Clean(request.TradeName);
        if (type == ClientTypes.Person && (firstName is null || lastName is null)) throw new ApiException(400, "Nombre y apellido son obligatorios para una persona.");
        if (type == ClientTypes.Company && legalName is null) throw new ApiException(400, "La razón social es obligatoria para una empresa.");
        client.Type = type; client.FirstName = type == ClientTypes.Person ? firstName : null; client.LastName = type == ClientTypes.Person ? lastName : null;
        client.LegalName = type == ClientTypes.Company ? legalName : null; client.TradeName = type == ClientTypes.Company ? tradeName : null;
        client.ContactPerson = type == ClientTypes.Company ? Clean(request.ContactPerson) : null;
        client.DisplayName = type == ClientTypes.Person ? $"{firstName} {lastName}" : tradeName ?? legalName!;
        (client.IdentificationType, client.IdentificationNumber) = ClientIdentificationPolicy.NormalizeAndValidate(type, request.IdentificationType, request.IdentificationNumber);
        client.Email = Clean(request.Email)?.ToLowerInvariant(); client.Phone = Clean(request.Phone); client.SecondaryPhone = Clean(request.SecondaryPhone);
        client.Address = Clean(request.Address); client.Notes = Clean(request.Notes); client.UpdatedAt = DateTime.UtcNow;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ClientDto ToDto(Client x) => new(x.Id, x.Type, x.DisplayName, x.IdentificationType, x.IdentificationNumber,
        x.Email, x.Phone, x.SecondaryPhone, x.Address, x.Notes, x.Status, x.FirstName, x.LastName, x.LegalName, x.TradeName,
        x.ContactPerson, x.CreatedAt, x.UpdatedAt);
}
