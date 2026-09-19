namespace LegalManagement.Domain;

public static class Roles
{
    public const string Admin = "ADMIN";
    public static readonly string[] All = [Admin, "LAWYER", "ASSISTANT", "READONLY"];
}

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = "";
}

public class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string RoleCode { get; set; } = "READONLY";
    public string Status { get; set; } = "ACTIVE";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Email { get; set; } = "";
    public string RoleCode { get; set; } = "READONLY";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = "";
    public string Status { get; set; } = "PENDING";
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public bool RememberMe { get; set; }
}
