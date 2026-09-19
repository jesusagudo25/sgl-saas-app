using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LegalManagement.Api;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(UserManager<ApplicationUser> users, AppDbContext db, AccessTokens accessTokens,
    IOptions<JwtOptions> jwt, IEmailSender emails, IConfiguration config, IWebHostEnvironment env,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string CookieName = "lm_refresh";
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private CookieOptions Cookie(bool remember = false, DateTime? expires = null) => new() {
        HttpOnly = true, Secure = !env.IsDevelopment() && !env.IsEnvironment("Testing"),
        SameSite = SameSiteMode.Strict, Path = "/api/auth", IsEssential = true,
        Expires = remember ? expires : null
    };
    private async Task<SessionDto> StartSession(ApplicationUser user, bool remember)
    {
        var raw = SecretTokens.Create();
        var token = new RefreshToken { UserId = user.Id, TokenHash = SecretTokens.Hash(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(jwt.Value.RefreshTokenDays), CreatedByIp = Ip, RememberMe = remember };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
        Response.Cookies.Append(CookieName, raw, Cookie(remember, token.ExpiresAt));
        return accessTokens.Issue(user);
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            Email = request.Email.Trim(), UserName = request.Email.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) throw new ApiException(400, "No se pudo crear la cuenta. Revisa el correo y usa una contraseña de al menos 10 caracteres, con mayúscula, minúscula, número y símbolo.");
        logger.LogInformation("UserRegistered UserId={UserId}", user.Id);
        return StatusCode(201, new UserDto(user.Id, user.FirstName, user.LastName, user.Email!));
    }

    [HttpPost("login")]
    public async Task<ActionResult<SessionDto>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, request.Password))
        {
            if (user is { IsActive: true } && !await users.IsLockedOutAsync(user)) await users.AccessFailedAsync(user);
            logger.LogWarning("LoginFailed");
            throw new ApiException(401, "Correo o contraseña incorrectos, o cuenta temporalmente bloqueada.");
        }
        await users.ResetAccessFailedCountAsync(user);
        // Revoke the previous browser session before replacing its cookie.
        await RevokeCookie();
        logger.LogInformation("LoginSucceeded UserId={UserId}", user.Id);
        return await StartSession(user, request.RememberMe);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<SessionDto>> Refresh()
    {
        var raw = Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw)) throw new ApiException(401, "Sesión expirada.");
        var hash = SecretTokens.Hash(raw);
        var old = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (old is null || old.RevokedAt != null || old.ExpiresAt <= DateTime.UtcNow)
            throw new ApiException(401, "Sesión expirada.");
        var user = await users.FindByIdAsync(old.UserId);
        if (user is null || !user.IsActive) throw new ApiException(401, "Sesión inválida.");
        var nextRaw = SecretTokens.Create();
        var replacement = new RefreshToken { UserId = old.UserId, TokenHash = SecretTokens.Hash(nextRaw),
            ExpiresAt = old.ExpiresAt, CreatedByIp = Ip, RememberMe = old.RememberMe };
        old.RevokedAt = DateTime.UtcNow;
        old.RevokedByIp = Ip;
        old.ReplacedByTokenId = replacement.Id;
        db.RefreshTokens.Add(replacement);
        // One atomic SaveChanges; concurrency token ensures only one request can consume the old token.
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ApiException(401, "Sesión ya renovada."); }
        Response.Cookies.Append(CookieName, nextRaw, Cookie(old.RememberMe, replacement.ExpiresAt));
        logger.LogInformation("RefreshTokenRevoked UserId={UserId}", old.UserId);
        return accessTokens.Issue(user);
    }

    private async Task RevokeCookie()
    {
        if (Request.Cookies[CookieName] is not { } raw) return;
        var hash = SecretTokens.Hash(raw);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
        if (token is null) return;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = Ip;
        await db.SaveChangesAsync();
        logger.LogInformation("RefreshTokenRevoked UserId={UserId}", token.UserId);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await RevokeCookie();
        Response.Cookies.Delete(CookieName, Cookie());
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { IsActive: true })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var url = $"{config["FrontendUrl"]!.TrimEnd('/')}/reset-password#email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
            await emails.SendAsync(user.Email!, "Recupera tu contraseña", url);
        }
        return Ok(new { message = "Si el correo corresponde a una cuenta activa, recibirás un enlace de recuperación." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive) throw new ApiException(400, "Enlace inválido o expirado.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded) throw new ApiException(400, "Enlace inválido o expirado, o contraseña insuficientemente segura.");
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow).SetProperty(t => t.RevokedByIp, Ip));
        await tx.CommitAsync();
        Response.Cookies.Delete(CookieName, Cookie());
        logger.LogInformation("RefreshTokenRevoked UserId={UserId}", user.Id);
        return NoContent();
    }

    [Authorize, HttpGet("/api/me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await users.FindByIdAsync(User.FindFirst("sub")!.Value);
        return new UserDto(user!.Id, user.FirstName, user.LastName, user.Email!);
    }
}
