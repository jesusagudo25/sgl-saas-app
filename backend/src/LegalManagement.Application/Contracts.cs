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
