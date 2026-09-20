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

public static class ClientTypes
{
    public const string Person = "PERSON";
    public const string Company = "COMPANY";
    public static readonly string[] All = [Person, Company];
}

public static class ClientStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public static readonly string[] All = [Active, Inactive];
}

public static class IdentificationTypes
{
    public const string Cedula = "CEDULA";
    public const string Passport = "PASSPORT";
    public const string Ruc = "RUC";
    public static readonly string[] Person = [Cedula, Passport];
    public static readonly string[] Company = [Ruc];
}

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Type { get; set; } = ClientTypes.Person;
    public string DisplayName { get; set; } = "";
    public string IdentificationType { get; set; } = "";
    public string IdentificationNumber { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? SecondaryPhone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = ClientStatuses.Active;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LegalName { get; set; }
    public string? TradeName { get; set; }
    public string? ContactPerson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class CaseStatuses
{
    public const string Open = "OPEN";
    public const string InProgress = "IN_PROGRESS";
    public const string Suspended = "SUSPENDED";
    public const string Closed = "CLOSED";
    public static readonly string[] All = [Open, InProgress, Suspended, Closed];
}

public static class CasePriorities
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string Urgent = "URGENT";
    public static readonly string[] All = [Low, Medium, High, Urgent];
}

public class Case
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string CaseNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string CaseType { get; set; } = "";
    public string Status { get; set; } = CaseStatuses.Open;
    public Guid? CaseStatusId { get; set; }
    public CaseStatus? CaseStatus { get; set; }
    public Guid? CaseTypeId { get; set; }
    public CaseType? CaseTypeReference { get; set; }
    public string Priority { get; set; } = CasePriorities.Medium;
    public Guid? ResponsibleMembershipId { get; set; }
    public Membership? ResponsibleMembership { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? SituationDate { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Court { get; set; }
    public Guid? CourtId { get; set; }
    public Court? CourtReference { get; set; }
    public string? Jurisdiction { get; set; }
    public Guid? JurisdictionId { get; set; }
    public Jurisdiction? JurisdictionReference { get; set; }
    public string? Counterparty { get; set; }
    public string? OpposingCounsel { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class OrganizationCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CaseStatus : OrganizationCatalog
{
    public string Code { get; set; } = "";
    public bool IsOpen { get; set; }
    public bool IsClosed { get; set; }
    public bool IsInnocent { get; set; }
    public bool IsGuilty { get; set; }
}
public class CaseType : OrganizationCatalog { }
public class Court : OrganizationCatalog { }
public class Jurisdiction : OrganizationCatalog { }
