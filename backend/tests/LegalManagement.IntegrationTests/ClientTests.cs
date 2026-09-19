using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class ClientTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private const string Password = "ValidPassw0rd!";
    private static object Person(string identification, string firstName = "Ana", string lastName = "Pérez") => new
    { type = "PERSON", identificationType = "CEDULA", identificationNumber = identification, email = "ana@example.test",
      phone = "6000-0000", secondaryPhone = "6000-0001", address = "Ciudad", notes = "Cliente", firstName, lastName };

    private async Task<(HttpClient Client, OrganizationDto Organization)> Setup(string prefix)
    {
        var client = factory.CreateClient(new() { HandleCookies = true }); client.DefaultRequestHeaders.Add("X-CSRF", "1");
        var email = $"{prefix}-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { firstName = "Test", lastName = "User", email, password = Password })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.PostAsJsonAsync("/api/organizations", new { name = $"Firma {Guid.NewGuid():N}" });
        var organization = (await response.Content.ReadFromJsonAsync<OrganizationDto>())!;
        client.DefaultRequestHeaders.Add("X-Organization-Id", organization.Id.ToString());
        return (client, organization);
    }

    private static async Task<ClientDto> Create(HttpClient client, string identification, string firstName = "Ana")
    {
        var response = await client.PostAsJsonAsync("/api/clients", Person(identification, firstName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ClientDto>())!;
    }

    [Fact]
    public async Task CreateClient_ShouldUseActiveTenant()
    {
        var (http, organization) = await Setup("create-tenant"); using (http) { var client = await Create(http, Guid.NewGuid().ToString("N"));
        using var scope = factory.Services.CreateScope(); var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clients.SingleAsync(x => x.Id == client.Id);
        Assert.Equal(organization.Id, saved.OrganizationId); }
    }

    [Fact]
    public async Task GetClients_ShouldOnlyReturnActiveTenantClients()
    {
        var (a, _) = await Setup("list-a"); var (b, _) = await Setup("list-b"); using (a) using (b) {
        var own = await Create(a, Guid.NewGuid().ToString("N"), "Propio"); var foreign = await Create(b, Guid.NewGuid().ToString("N"), "Ajeno");
        var page = await a.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients"); Assert.Contains(page!.Items, x => x.Id == own.Id); Assert.DoesNotContain(page.Items, x => x.Id == foreign.Id); }
    }

    [Fact]
    public async Task GetClient_ShouldReturnOwnTenantClient()
    {
        var (http, _) = await Setup("get-own"); using (http) { var created = await Create(http, Guid.NewGuid().ToString("N"));
        var found = await http.GetFromJsonAsync<ClientDto>($"/api/clients/{created.Id}"); Assert.Equal(created.Id, found!.Id); }
    }

    [Fact]
    public async Task GetClient_ShouldRejectForeignTenantClient()
    {
        var (a, _) = await Setup("foreign-a"); var (b, _) = await Setup("foreign-b"); using (a) using (b) {
        var foreign = await Create(b, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/clients/{foreign.Id}")).StatusCode); }
    }

    [Fact]
    public async Task UpdateClient_ShouldUpdateOwnTenantClient()
    {
        var (http, _) = await Setup("update"); using (http) { var created = await Create(http, Guid.NewGuid().ToString("N"));
        var response = await http.PutAsJsonAsync($"/api/clients/{created.Id}", Person(created.IdentificationNumber, "María", "Actualizada"));
        response.EnsureSuccessStatusCode(); var updated = await response.Content.ReadFromJsonAsync<ClientDto>(); Assert.Equal("María Actualizada", updated!.DisplayName); }
    }

    [Fact]
    public async Task ChangeClientStatus_ShouldDeactivateClient()
    {
        var (http, _) = await Setup("status"); using (http) { var created = await Create(http, Guid.NewGuid().ToString("N"));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/clients/{created.Id}/status") { Content = JsonContent.Create(new { status = "INACTIVE" }) };
        var response = await http.SendAsync(request); response.EnsureSuccessStatusCode(); Assert.Equal("INACTIVE", (await response.Content.ReadFromJsonAsync<ClientDto>())!.Status); }
    }

    [Fact]
    public async Task CreateClient_ShouldRejectDuplicateIdentificationInsideOrganization()
    {
        var (http, _) = await Setup("duplicate"); using (http) { var id = Guid.NewGuid().ToString("N"); await Create(http, id);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/clients", Person(id, "Otra"))).StatusCode); }
    }

    [Fact]
    public async Task CreateClient_ShouldAllowSameIdentificationInDifferentOrganizations()
    {
        var (a, _) = await Setup("same-a"); var (b, _) = await Setup("same-b"); using (a) using (b) { var id = Guid.NewGuid().ToString("N");
        Assert.NotNull(await Create(a, id)); Assert.NotNull(await Create(b, id)); }
    }
}
