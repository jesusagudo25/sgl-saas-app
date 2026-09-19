using System.Text.RegularExpressions;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Authorize, Route("api/organizations")]
public class OrganizationsController(AppDbContext db, ITenantContext tenant, IEmailSender emails,
    IConfiguration config, ILogger<OrganizationsController> logger) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [HttpGet]
    public async Task<ActionResult<List<OrganizationDto>>> List() => await db.Memberships.AsNoTracking()
        .Where(m => m.UserId == UserId && m.Status == "ACTIVE" && m.Organization.Status == "ACTIVE")
        .OrderBy(m => m.Organization.Name)
        .Select(m => new OrganizationDto(m.OrganizationId, m.Organization.Name, m.Organization.Slug, m.RoleCode)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<OrganizationDto>> Create(CreateOrganizationRequest request)
    {
        var name = request.Name.Trim();
        var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var org = new Organization { Name = name, Slug = $"{slug}-{Guid.NewGuid():N}", CreatedByUserId = UserId };
        db.Organizations.Add(org);
        db.Memberships.Add(new() { UserId = UserId, Organization = org, RoleCode = Roles.Admin });
        // EF wraps both inserts in one transaction.
        await db.SaveChangesAsync();
        logger.LogInformation("OrganizationCreated UserId={UserId} OrganizationId={OrganizationId}", UserId, org.Id);
        return StatusCode(201, new OrganizationDto(org.Id, org.Name, org.Slug, Roles.Admin));
    }

    [HttpGet("{id:guid}"), TenantRequired, Authorize(Policy = "OrganizationMember")]
    public async Task<ActionResult<OrganizationDto>> Get(Guid id)
    {
        var org = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == tenant.OrganizationId);
        logger.LogInformation("OrganizationSelected UserId={UserId} OrganizationId={OrganizationId}", tenant.UserId, tenant.OrganizationId);
        return new OrganizationDto(org.Id, org.Name, org.Slug, tenant.RoleCode);
    }

    [HttpGet("{id:guid}/members"), TenantRequired, Authorize(Policy = "OrganizationMember")]
    public async Task<ActionResult<List<MemberDto>>> Members(Guid id) => await (
        from m in db.Memberships.AsNoTracking()
        join u in db.Users on m.UserId equals u.Id
        where m.OrganizationId == tenant.OrganizationId
        orderby u.FirstName, u.LastName
        select new MemberDto(u.Id, u.FirstName, u.LastName, u.Email!, m.RoleCode, m.Status)).ToListAsync();

    [HttpPost("{id:guid}/invitations"), TenantRequired, Authorize(Policy = "OrganizationAdmin")]
    public async Task<ActionResult<InvitationDto>> Invite(Guid id, InviteRequest request)
    {
        if (!Roles.All.Contains(request.RoleCode)) throw new ApiException(400, "Rol inválido.");
        var email = request.Email.Trim().ToUpperInvariant();
        if (await (from m in db.Memberships join u in db.Users on m.UserId equals u.Id
            where m.OrganizationId == tenant.OrganizationId && u.NormalizedEmail == email select m.Id).AnyAsync())
            throw new ApiException(409, "El usuario ya tiene una membresía en esta organización.");
        var pending = await db.Invitations.SingleOrDefaultAsync(i => i.OrganizationId == tenant.OrganizationId && i.Email == email && i.Status == "PENDING");
        if (pending is not null && pending.ExpiresAt > DateTime.UtcNow)
            throw new ApiException(409, "Ya existe una invitación pendiente para este correo.");
        await using var tx = await db.Database.BeginTransactionAsync();
        if (pending is not null) { pending.Status = "EXPIRED"; await db.SaveChangesAsync(); }
        var raw = SecretTokens.Create();
        var invitation = new Invitation { OrganizationId = tenant.OrganizationId, Email = email, RoleCode = request.RoleCode,
            TokenHash = SecretTokens.Hash(raw), ExpiresAt = DateTime.UtcNow.AddHours(config.GetValue("Invitations:ExpirationHours", 72)),
            CreatedByUserId = tenant.UserId };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
        var org = await db.Organizations.SingleAsync(o => o.Id == tenant.OrganizationId);
        await emails.SendAsync(email, $"Invitación a {org.Name}", $"{config["FrontendUrl"]!.TrimEnd('/')}/invitations/{raw}");
        await tx.CommitAsync();
        logger.LogInformation("InvitationCreated InvitationId={InvitationId} OrganizationId={OrganizationId}", invitation.Id, tenant.OrganizationId);
        return StatusCode(201, new InvitationDto(invitation.Id, org.Name, email, invitation.RoleCode, invitation.ExpiresAt));
    }
}
