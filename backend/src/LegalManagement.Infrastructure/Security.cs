using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LegalManagement.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LegalManagement.Infrastructure;

public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public static class SecretTokens
{
    public static string Create() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public class AccessTokens(IOptions<JwtOptions> options)
{
    public SessionDto Issue(ApplicationUser user)
    {
        var o = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(o.AccessTokenMinutes);
        var token = new JwtSecurityToken(o.Issuer, o.Audience,
            [new Claim("sub", user.Id), new Claim("email", user.Email!),
             new Claim("jti", Guid.NewGuid().ToString()), new Claim("sst", user.SecurityStamp!)],
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires,
            new(user.Id, user.FirstName, user.LastName, user.Email!));
    }
}

// Volatile development mailbox: URLs never enter logs or durable storage.
// Exposed only by a Development-only endpoint restricted to loopback requests.
public record DevelopmentEmail(Guid Id, string Email, string Subject, string Url, DateTime CreatedAt);
public class DevelopmentEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<DevelopmentEmail> messages = new();
    public IReadOnlyList<DevelopmentEmail> Messages => messages.Reverse().ToArray();
    public Task SendAsync(string email, string subject, string url)
    {
        messages.Enqueue(new(Guid.NewGuid(), email, subject, url, DateTime.UtcNow));
        while (messages.Count > 100) messages.TryDequeue(out _);
        return Task.CompletedTask;
    }
}
