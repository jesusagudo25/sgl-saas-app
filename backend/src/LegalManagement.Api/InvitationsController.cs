using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Api;

[ApiController, Route("api/invitations"), EnableRateLimiting("auth")]
public class InvitationsController(AppDbContext db, ILogger<InvitationsController> logger) : ControllerBase
{
    private async Task<Invitation> Find(string token)
    {
        if (token.Length > 128) throw new ApiException(404, "Invitación no encontrada.");
        var hash = SecretTokens.Hash(token);
        var invitation = await db.Invitations.Include(i => i.Organization).SingleOrDefaultAsync(i => i.TokenHash == hash);
        if (invitation is null) throw new ApiException(404, "Invitación no encontrada.");
        if (invitation.Status == "PENDING" && invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = "EXPIRED";
            await db.SaveChangesAsync();
            throw new ApiException(410, "La invitación ha expirado o ya no está disponible.");
        }
        if (invitation.Status != "PENDING" || invitation.Organization.Status != "ACTIVE")
            throw new ApiException(410, "La invitación ha expirado o ya no está disponible.");
        return invitation;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<InvitationDto>> Get(string token)
    {
        var i = await Find(token);
        return new InvitationDto(i.Id, i.Organization.Name, i.Email, i.RoleCode, i.ExpiresAt);
    }

    [Authorize, HttpPost("{token}/accept")]
    public async Task<ActionResult<OrganizationDto>> Accept(string token)
    {
        var i = await Find(token);
        var userId = User.FindFirst("sub")!.Value;
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        if (user.NormalizedEmail != i.Email) throw new ApiException(403, "Inicia sesión con el correo que recibió la invitación.");
        if (await db.Memberships.AnyAsync(m => m.UserId == userId && m.OrganizationId == i.OrganizationId))
            throw new ApiException(409, "Ya tienes una membresía en esta organización.");
        i.Status = "ACCEPTED";
        i.AcceptedAt = DateTime.UtcNow;
        db.Memberships.Add(new() { UserId = userId, OrganizationId = i.OrganizationId, RoleCode = i.RoleCode });
        // Unique membership and invitation concurrency token make acceptance atomic and single-use.
        await db.SaveChangesAsync();
        logger.LogInformation("InvitationAccepted InvitationId={InvitationId} UserId={UserId}", i.Id, userId);
        return new OrganizationDto(i.OrganizationId, i.Organization.Name, i.Organization.Slug, i.RoleCode);
    }
}
