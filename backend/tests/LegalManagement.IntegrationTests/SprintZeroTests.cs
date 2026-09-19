using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class SprintZeroTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private const string Password = "ValidPassw0rd!";

    private HttpClient Client()
    {
        var client = factory.CreateClient(new() { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return client;
    }

    private static string Email(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    private static async Task Register(HttpClient client, string email, string firstName = "Test", string lastName = "User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { firstName, lastName, email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<SessionDto> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password, rememberMe = false });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotNull(session);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return session;
    }

    private static async Task<OrganizationDto> CreateOrganization(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/organizations", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrganizationDto>())!;
    }

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private string InvitationTokenFor(string email)
    {
        var sender = factory.Services.GetRequiredService<DevelopmentEmailSender>();
        var message = sender.Messages.First(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return new Uri(message.Url).Segments.Last();
    }

    [Fact]
    public async Task Register_ShouldCreateUser()
    {
        using var client = Client();
        var email = Email("register");

        await Register(client, email, "Ana", "Registro");

        var user = await WithDb(db => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant()));
        Assert.NotNull(user);
        Assert.Equal("Ana", user.FirstName);
        Assert.NotNull(user.PasswordHash);
    }

    [Fact]
    public async Task Login_ShouldReturnValidSession()
    {
        using var client = Client();
        var email = Email("login");
        await Register(client, email);

        var session = await Login(client, email);
        var me = await client.GetAsync("/api/me");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.True(session.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task RefreshAndLogout_ShouldRotateAndRevokeSession()
    {
        using var client = Client();
        var email = Email("refresh");
        await Register(client, email);
        var first = await Login(client, email);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", null);
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(first.AccessToken, refreshed.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task ForgotAndResetPassword_ShouldReplacePasswordAndRevokeSessions()
    {
        using var client = Client();
        var email = Email("password-reset");
        await Register(client, email);
        await Login(client, email);
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).EnsureSuccessStatusCode();
        var sender = factory.Services.GetRequiredService<DevelopmentEmailSender>();
        var resetUrl = new Uri(sender.Messages.First(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase)).Url);
        var values = resetUrl.Fragment.TrimStart('#').Split('&')
            .Select(part => part.Split('=', 2)).ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
        const string newPassword = "NewValidPassw0rd!";

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            email = values["email"], token = values["token"], password = newPassword
        });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword })).StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_ShouldCreateAdminMembership()
    {
        using var client = Client();
        var email = Email("organization");
        await Register(client, email);
        var session = await Login(client, email);

        var organization = await CreateOrganization(client, $"Firma {Guid.NewGuid():N}");

        var membership = await WithDb(db => db.Memberships.AsNoTracking()
            .SingleOrDefaultAsync(m => m.UserId == session.User.Id && m.OrganizationId == organization.Id));
        Assert.NotNull(membership);
        Assert.Equal("ADMIN", membership.RoleCode);
        Assert.Equal("ACTIVE", membership.Status);
    }

    [Fact]
    public async Task GetOrganizations_ShouldOnlyReturnUserMemberships()
    {
        using var userA = Client();
        using var userB = Client();
        var emailA = Email("list-a");
        var emailB = Email("list-b");
        await Register(userA, emailA);
        await Register(userB, emailB);
        await Login(userA, emailA);
        await Login(userB, emailB);
        var organizationA = await CreateOrganization(userA, $"Firma A {Guid.NewGuid():N}");
        var organizationB = await CreateOrganization(userB, $"Firma B {Guid.NewGuid():N}");

        var organizations = await userA.GetFromJsonAsync<List<OrganizationDto>>("/api/organizations");

        Assert.Contains(organizations!, o => o.Id == organizationA.Id);
        Assert.DoesNotContain(organizations!, o => o.Id == organizationB.Id);
    }

    [Fact]
    public async Task AcceptInvitation_ShouldCreateMembershipWithInvitedRole()
    {
        using var admin = Client();
        using var invited = Client();
        var adminEmail = Email("invite-admin");
        var invitedEmail = Email("invite-lawyer");
        await Register(admin, adminEmail);
        await Register(invited, invitedEmail);
        await Login(admin, adminEmail);
        var organization = await CreateOrganization(admin, $"Invitación {Guid.NewGuid():N}");
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/organizations/{organization.Id}/invitations")
        {
            Content = JsonContent.Create(new { email = invitedEmail, roleCode = "LAWYER" })
        };
        inviteRequest.Headers.Add("X-Organization-Id", organization.Id.ToString());
        var inviteResponse = await admin.SendAsync(inviteRequest);
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var token = InvitationTokenFor(invitedEmail);
        var invitedSession = await Login(invited, invitedEmail);

        var accept = await invited.PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var membership = await WithDb(db => db.Memberships.AsNoTracking()
            .SingleAsync(m => m.UserId == invitedSession.User.Id && m.OrganizationId == organization.Id));
        Assert.Equal("LAWYER", membership.RoleCode);
        Assert.Equal("ACTIVE", membership.Status);
    }

    [Fact]
    public async Task TenantIsolation_ShouldReturn403ForForeignOrganization()
    {
        using var userA = Client();
        using var userB = Client();
        var emailA = Email("tenant-a");
        var emailB = Email("tenant-b");
        await Register(userA, emailA);
        await Register(userB, emailB);
        await Login(userA, emailA);
        await Login(userB, emailB);
        await CreateOrganization(userA, $"Tenant A {Guid.NewGuid():N}");
        var organizationB = await CreateOrganization(userB, $"Tenant B {Guid.NewGuid():N}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{organizationB.Id}");
        request.Headers.Add("X-Organization-Id", organizationB.Id.ToString());

        var response = await userA.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredInvitation_ShouldPersistExpiredStatusAndRejectAcceptance()
    {
        using var admin = Client();
        using var invited = Client();
        var adminEmail = Email("expired-admin");
        var invitedEmail = Email("expired-user");
        await Register(admin, adminEmail);
        await Register(invited, invitedEmail);
        await Login(admin, adminEmail);
        var organization = await CreateOrganization(admin, $"Expirada {Guid.NewGuid():N}");
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/organizations/{organization.Id}/invitations")
        {
            Content = JsonContent.Create(new { email = invitedEmail, roleCode = "ASSISTANT" })
        };
        inviteRequest.Headers.Add("X-Organization-Id", organization.Id.ToString());
        (await admin.SendAsync(inviteRequest)).EnsureSuccessStatusCode();
        var token = InvitationTokenFor(invitedEmail);
        await WithDb(async db =>
        {
            var invitation = await db.Invitations.SingleAsync(i => i.Email == invitedEmail.ToUpperInvariant());
            invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return true;
        });
        await Login(invited, invitedEmail);

        var response = await invited.PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var status = await WithDb(db => db.Invitations.AsNoTracking()
            .Where(i => i.Email == invitedEmail.ToUpperInvariant()).Select(i => i.Status).SingleAsync());
        Assert.Equal("EXPIRED", status);
    }

    [Fact]
    public async Task CompleteMultiTenantFlow_ShouldAllowMembershipSwitchAndDenyForeignTenant()
    {
        using var userA = Client();
        using var userB = Client();
        var emailA = Email("flow-a");
        var emailB = Email("flow-b");
        await Register(userA, emailA, "Usuario", "A");
        await Login(userA, emailA);
        var organizationA = await CreateOrganization(userA, "Bufete A");
        using var inviteRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/organizations/{organizationA.Id}/invitations")
        {
            Content = JsonContent.Create(new { email = emailB, roleCode = "LAWYER" })
        };
        inviteRequest.Headers.Add("X-Organization-Id", organizationA.Id.ToString());
        (await userA.SendAsync(inviteRequest)).EnsureSuccessStatusCode();
        var token = InvitationTokenFor(emailB);
        await Register(userB, emailB, "Usuario", "B");
        await Login(userB, emailB);
        (await userB.PostAsync($"/api/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
        var organizationB = await CreateOrganization(userB, "Bufete B");

        using var enterA = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{organizationA.Id}");
        enterA.Headers.Add("X-Organization-Id", organizationA.Id.ToString());
        Assert.Equal(HttpStatusCode.OK, (await userB.SendAsync(enterA)).StatusCode);
        using var enterB = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{organizationB.Id}");
        enterB.Headers.Add("X-Organization-Id", organizationB.Id.ToString());
        Assert.Equal(HttpStatusCode.OK, (await userB.SendAsync(enterB)).StatusCode);
        using var forbidden = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{organizationB.Id}");
        forbidden.Headers.Add("X-Organization-Id", organizationB.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await userA.SendAsync(forbidden)).StatusCode);

        var roles = await WithDb(db => db.Memberships.AsNoTracking()
            .Where(m => m.UserId == db.Users.Where(u => u.NormalizedEmail == emailB.ToUpperInvariant()).Select(u => u.Id).Single())
            .ToDictionaryAsync(m => m.OrganizationId, m => m.RoleCode));
        Assert.Equal("LAWYER", roles[organizationA.Id]);
        Assert.Equal("ADMIN", roles[organizationB.Id]);
    }
}
