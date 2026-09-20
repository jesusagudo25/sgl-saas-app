using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class CaseTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private const string Password = "ValidPassw0rd!";

    private async Task<(HttpClient Http, OrganizationDto Organization, Membership Membership)> Setup(string prefix)
    {
        var http = factory.CreateClient(new() { HandleCookies = true }); http.DefaultRequestHeaders.Add("X-CSRF", "1");
        var email = $"{prefix}-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await http.PostAsJsonAsync("/api/auth/register", new { firstName = "Test", lastName = "Lawyer", email, password = Password })).StatusCode);
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var orgResponse = await http.PostAsJsonAsync("/api/organizations", new { name = $"Firma {Guid.NewGuid():N}" });
        var org = (await orgResponse.Content.ReadFromJsonAsync<OrganizationDto>())!;
        http.DefaultRequestHeaders.Add("X-Organization-Id", org.Id.ToString());
        using var scope = factory.Services.CreateScope();
        var membership = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Memberships.AsNoTracking().SingleAsync(x => x.OrganizationId == org.Id);
        return (http, org, membership);
    }

    private static object Person(string identification) => new { type = "PERSON", identificationType = "CEDULA", identificationNumber = identification,
        firstName = "Ana", lastName = "Perez" };
    private static async Task<ClientDto> CreateClient(HttpClient http)
    {
        var response = await http.PostAsJsonAsync("/api/clients", Person(Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ClientDto>())!;
    }
    private static object Payload(Guid clientId, string number, Guid? responsible = null, string status = "OPEN") => new {
        clientId, caseNumber = number, title = "Caso de prueba", description = "Descripcion", caseType = "CIVIL", status,
        priority = "HIGH", responsibleMembershipId = responsible, situationDate = DateTime.UtcNow.Date.AddDays(-2), openedAt = DateTime.UtcNow.Date,
        court = "Juzgado Primero", jurisdiction = "Panama", counterparty = "Contraparte", opposingCounsel = "Abogado", notes = "Notas" };
    private static async Task<CaseDto> CreateCase(HttpClient http, Guid clientId, string? number = null, Guid? responsible = null)
    {
        var response = await http.PostAsJsonAsync("/api/cases", Payload(clientId, number ?? Guid.NewGuid().ToString("N"), responsible));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CaseDto>())!;
    }

    [Fact] public async Task CreateCase_ShouldUseClientFromActiveTenant()
    { var (http, org, _) = await Setup("case-client"); using (http) { var client = await CreateClient(http); var created = await CreateCase(http, client.Id);
      using var scope = factory.Services.CreateScope(); var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cases.SingleAsync(x => x.Id == created.Id);
      Assert.Equal(org.Id, saved.OrganizationId); Assert.Equal(client.Id, saved.ClientId); } }

    [Fact] public async Task CreateCase_ShouldRejectForeignTenantClient()
    { var (a, _, _) = await Setup("foreign-client-a"); var (b, _, _) = await Setup("foreign-client-b"); using (a) using (b) {
      var foreign = await CreateClient(b); Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/cases", Payload(foreign.Id, "F-1"))).StatusCode); } }

    [Fact] public async Task CreateCase_ShouldAcceptResponsibleMembershipFromActiveTenant()
    { var (http, _, membership) = await Setup("own-member"); using (http) { var client = await CreateClient(http);
      var created = await CreateCase(http, client.Id, responsible: membership.Id); Assert.Equal(membership.Id, created.ResponsibleMembershipId); } }

    [Fact] public async Task CreateCase_ShouldRejectForeignTenantResponsibleMembership()
    { var (a, _, _) = await Setup("foreign-member-a"); var (b, _, foreign) = await Setup("foreign-member-b"); using (a) using (b) {
      var client = await CreateClient(a); Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/cases", Payload(client.Id, "F-2", foreign.Id))).StatusCode); } }

    [Fact] public async Task GetCases_ShouldOnlyReturnActiveTenantCases()
    { var (a, _, _) = await Setup("list-case-a"); var (b, _, _) = await Setup("list-case-b"); using (a) using (b) {
      var own = await CreateCase(a, (await CreateClient(a)).Id); var foreign = await CreateCase(b, (await CreateClient(b)).Id);
      var page = await a.GetFromJsonAsync<PagedResult<CaseDto>>("/api/cases"); Assert.Contains(page!.Items, x => x.Id == own.Id); Assert.DoesNotContain(page.Items, x => x.Id == foreign.Id); } }

    [Fact] public async Task GetCase_ShouldReturnOwnTenantCase()
    { var (http, _, _) = await Setup("get-case"); using (http) { var created = await CreateCase(http, (await CreateClient(http)).Id);
      Assert.Equal(created.Id, (await http.GetFromJsonAsync<CaseDto>($"/api/cases/{created.Id}"))!.Id); } }

    [Fact] public async Task GetCase_ShouldRejectForeignTenantCase()
    { var (a, _, _) = await Setup("get-foreign-a"); var (b, _, _) = await Setup("get-foreign-b"); using (a) using (b) {
      var foreign = await CreateCase(b, (await CreateClient(b)).Id); Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/cases/{foreign.Id}")).StatusCode); } }

    [Fact] public async Task UpdateCase_ShouldUpdateOwnTenantCase()
    { var (http, _, _) = await Setup("update-case"); using (http) { var client = await CreateClient(http); var created = await CreateCase(http, client.Id);
      var response = await http.PutAsJsonAsync($"/api/cases/{created.Id}", Payload(client.Id, created.CaseNumber, status: "IN_PROGRESS")); response.EnsureSuccessStatusCode();
      var updated = (await response.Content.ReadFromJsonAsync<CaseDto>())!; Assert.Equal("IN_PROGRESS", updated.Status); } }

    [Fact] public async Task CloseCase_ShouldSetClosedAt()
    { var (http, _, _) = await Setup("close-case"); using (http) { var created = await CreateCase(http, (await CreateClient(http)).Id);
      var response = await http.PatchAsJsonAsync($"/api/cases/{created.Id}/status", new { status = "CLOSED" }); response.EnsureSuccessStatusCode();
      Assert.NotNull((await response.Content.ReadFromJsonAsync<CaseDto>())!.ClosedAt); } }

    [Fact] public async Task ReopenCase_ShouldClearClosedAt()
    { var (http, _, _) = await Setup("reopen-case"); using (http) { var created = await CreateCase(http, (await CreateClient(http)).Id);
      await http.PatchAsJsonAsync($"/api/cases/{created.Id}/status", new { status = "CLOSED" });
      var response = await http.PatchAsJsonAsync($"/api/cases/{created.Id}/status", new { status = "OPEN" }); response.EnsureSuccessStatusCode();
      Assert.Null((await response.Content.ReadFromJsonAsync<CaseDto>())!.ClosedAt); } }

    [Fact] public async Task CreateCase_ShouldRejectDuplicateCaseNumberInsideOrganization()
    { var (http, _, _) = await Setup("duplicate-case"); using (http) { var client = await CreateClient(http); await CreateCase(http, client.Id, "EXP-100");
      Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/cases", Payload(client.Id, "exp-100"))).StatusCode); } }

    [Fact] public async Task CreateCase_ShouldAllowSameCaseNumberInDifferentOrganizations()
    { var (a, _, _) = await Setup("same-case-a"); var (b, _, _) = await Setup("same-case-b"); using (a) using (b) {
      await CreateCase(a, (await CreateClient(a)).Id, "EXP-200"); await CreateCase(b, (await CreateClient(b)).Id, "EXP-200"); } }

    [Fact] public async Task SituationDate_ShouldPersistAndUpdate()
    { var (http, _, _) = await Setup("situation-date"); using (http) { var client = await CreateClient(http); var created = await CreateCase(http, client.Id);
      Assert.Equal(DateTime.UtcNow.Date.AddDays(-2), created.SituationDate?.Date);
      var payload = new { clientId = client.Id, caseNumber = created.CaseNumber, title = created.Title, caseType = "CIVIL", status = "OPEN",
          priority = "HIGH", openedAt = DateTime.UtcNow.Date, situationDate = DateTime.UtcNow.Date.AddDays(-5) };
      var response = await http.PutAsJsonAsync($"/api/cases/{created.Id}", payload); response.EnsureSuccessStatusCode();
      Assert.Equal(DateTime.UtcNow.Date.AddDays(-5), (await response.Content.ReadFromJsonAsync<CaseDto>())!.SituationDate?.Date); } }
}
