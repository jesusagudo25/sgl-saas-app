using System.ComponentModel.DataAnnotations;

namespace LegalManagement.Application;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    string UserId { get; }
    string RoleCode { get; }
    bool IsResolved { get; }
}

public interface IEmailSender
{
    Task SendAsync(string email, string subject, string url);
}

public record RegisterRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 10)] string Password);
public record LoginRequest([Required, EmailAddress] string Email, [Required, StringLength(128)] string Password, bool RememberMe = false);
public record ForgotPasswordRequest([Required, EmailAddress] string Email);
public record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token,
    [Required, StringLength(128, MinimumLength = 10)] string Password);
public record CreateOrganizationRequest([Required, StringLength(160)] string Name);
public record InviteRequest([Required, EmailAddress, StringLength(256)] string Email, [Required] string RoleCode);
public record UserDto(string Id, string FirstName, string LastName, string Email);
public record SessionDto(string AccessToken, DateTime ExpiresAt, UserDto User);
public record OrganizationDto(Guid Id, string Name, string Slug, string RoleCode);
public record MemberDto(string UserId, string FirstName, string LastName, string Email, string RoleCode, string Status);
public record InvitationDto(Guid Id, string OrganizationName, string Email, string RoleCode, DateTime ExpiresAt);

public record ClientRequest(
    [Required] string Type,
    [Required, StringLength(40)] string IdentificationType,
    [Required, StringLength(80)] string IdentificationNumber,
    [EmailAddress, StringLength(256)] string? Email,
    [StringLength(40)] string? Phone,
    [StringLength(40)] string? SecondaryPhone,
    [StringLength(500)] string? Address,
    [StringLength(2000)] string? Notes,
    [StringLength(120)] string? FirstName,
    [StringLength(120)] string? LastName,
    [StringLength(240)] string? LegalName,
    [StringLength(240)] string? TradeName,
    [StringLength(240)] string? ContactPerson);
public record ChangeClientStatusRequest([Required] string Status);
public record ClientDto(Guid Id, string Type, string DisplayName, string IdentificationType,
    string IdentificationNumber, string? Email, string? Phone, string? SecondaryPhone, string? Address,
    string? Notes, string Status, string? FirstName, string? LastName, string? LegalName, string? TradeName,
    string? ContactPerson, DateTime CreatedAt, DateTime UpdatedAt);
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record CaseRequest(
    [Required, StringLength(80)] string CaseNumber,
    [Required, StringLength(240)] string Title,
    [Required] Guid ClientId,
    [StringLength(120)] string? CaseType,
    [StringLength(4000)] string? Description,
    [Required] string Status,
    [Required] string Priority,
    Guid? ResponsibleMembershipId,
    [Required] DateTime OpenedAt,
    [StringLength(240)] string? Court,
    [StringLength(240)] string? Jurisdiction,
    [StringLength(240)] string? Counterparty,
    [StringLength(240)] string? OpposingCounsel,
    [StringLength(4000)] string? Notes);
public record ChangeCaseStatusRequest([Required] string Status);
public record CaseDto(Guid Id, Guid ClientId, string ClientName, string CaseNumber, string Title, string? Description,
    string CaseType, string Status, string Priority, Guid? ResponsibleMembershipId, string? ResponsibleName,
    DateTime OpenedAt, DateTime? ClosedAt, string? Court, string? Jurisdiction, string? Counterparty,
    string? OpposingCounsel, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);
public record CaseOptionDto(Guid Id, string Name);
public record CaseOptionsDto(IReadOnlyList<CaseOptionDto> Clients, IReadOnlyList<CaseOptionDto> ResponsibleMemberships);
