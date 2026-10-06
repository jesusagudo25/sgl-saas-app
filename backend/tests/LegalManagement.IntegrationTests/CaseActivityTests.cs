using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class CaseActivityTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private record Fixture(HttpClient Http, Guid Org, Guid Member, Guid Case, Guid Status, Guid General, Guid Detail);
    private async Task<Fixture> Setup()
    {
        var http = factory.CreateClient(new() { HandleCookies = true }); http.DefaultRequestHeaders.Add("X-CSRF", "1");
        var email = $"activity-{Guid.NewGuid():N}@example.test";
        (await http.PostAsJsonAsync("/api/auth/register", new { firstName = "Ana", lastName = "Perez", email, password = "ValidPassw0rd!" })).EnsureSuccessStatusCode();
        var session = await (await http.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPassw0rd!" })).Content.ReadFromJsonAsync<SessionDto>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var org = await (await http.PostAsJsonAsync("/api/organizations", new { name = $"Firma {Guid.NewGuid():N}" })).Content.ReadFromJsonAsync<OrganizationDto>();
        http.DefaultRequestHeaders.Add("X-Organization-Id", org!.Id.ToString());
        var status = await Created(http, "/api/settings/case-statuses", new { name = "Estado de seguimiento", code = "FOLLOWUP", isOpen = true });
        var client = await Created(http, "/api/clients", new { type = "PERSON", identificationType = "PASSPORT", identificationNumber = Guid.NewGuid().ToString("N"), firstName = "Ana", lastName = "Perez" });
        var legalCase = await Created(http, "/api/cases", new { clientId = Id(client), caseNumber = Guid.NewGuid().ToString("N"), title = "Caso", caseType = "CIVIL", status = "OPEN", priority = "MEDIUM", openedAt = DateTime.UtcNow });
        var general = await Created(http, "/api/settings/competences", new { name = "Civil" });
        var detail = await Created(http, "/api/settings/competences", new { name = "Contratos", parentId = Id(general) });
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new(http, org.Id, await db.Memberships.Where(x => x.OrganizationId == org.Id).Select(x => x.Id).SingleAsync(), Id(legalCase), Id(status), Id(general), Id(detail));
    }
    private static Guid Id(JsonElement value) => value.GetProperty("id").GetGuid();
    private static async Task<JsonElement> Created(HttpClient http, string path, object body)
    { var response = await http.PostAsJsonAsync(path, body); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static object FollowUp(Fixture f, DateTime? occurred = null) => new { caseStatusId = f.Status, competenceId = f.General, competenceDetailId = f.Detail, description = "Actuación judicial", occurredAt = occurred ?? DateTime.UtcNow };
    private static object TaskBody(Guid? member = null, string status = "PENDING", string priority = "HIGH", DateTime? due = null) => new { title = "Preparar escrito", description = "Detalle", assignedMembershipId = member, status, priority, dueAt = due };
    private static async Task Patch(HttpClient http, string path, object body) => (await http.PatchAsJsonAsync(path, body)).EnsureSuccessStatusCode();

    [Fact]
    public async Task FollowUps_ShouldCreateListOrderAndPreserveHistoricalSnapshot()
    {
        var f = await Setup(); using var http = f.Http; var path = $"/api/cases/{f.Case}/follow-ups";
        var before = await http.GetFromJsonAsync<JsonElement>($"/api/cases/{f.Case}");
        var old = await Created(http, path, FollowUp(f, DateTime.UtcNow.AddDays(-2))); var recent = await Created(http, path, FollowUp(f));
        var list = await http.GetFromJsonAsync<JsonElement[]>(path); Assert.Equal(new[] { Id(recent), Id(old) }, list!.Select(Id));
        Assert.Equal(f.Member, recent.GetProperty("createdByMembershipId").GetGuid()); Assert.Equal("Ana Perez", recent.GetProperty("createdBy").GetString());
        Assert.Equal(Id(recent), Id(await http.GetFromJsonAsync<JsonElement>($"{path}/{Id(recent)}")));
        (await http.PutAsJsonAsync($"/api/settings/competences/{f.General}", new { name = "Renombrada" })).EnsureSuccessStatusCode();
        foreach (var pair in new[] { ("competences", f.General), ("competences", f.Detail), ("case-statuses", f.Status) }) await Patch(http, $"/api/settings/{pair.Item1}/{pair.Item2}/status", new { isActive = false });
        var historical = await http.GetFromJsonAsync<JsonElement>($"{path}/{Id(old)}"); Assert.Equal("Civil", historical.GetProperty("competence").GetString()); Assert.Equal(f.Detail, historical.GetProperty("competenceDetailId").GetGuid());
        Assert.Equal("Estado de seguimiento", historical.GetProperty("status").GetString());
        var after = await http.GetFromJsonAsync<JsonElement>($"/api/cases/{f.Case}"); Assert.Equal(before.GetProperty("caseStatusId").ToString(), after.GetProperty("caseStatusId").ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(path, FollowUp(f))).StatusCode);
        var anotherRoot = await Created(http, "/api/settings/competences", new { name = "Nueva raíz" });
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync($"/api/settings/competences/{f.Detail}", new { name = "Contratos", parentId = Id(anotherRoot) })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LegalCompetences.Remove(await db.LegalCompetences.SingleAsync(x => x.Id == f.Detail)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("case")]
    [InlineData("status")]
    [InlineData("general")]
    [InlineData("detail")]
    [InlineData("wrong-parent")]
    public async Task FollowUps_ShouldRejectForeignAndIncorrectRelations(string relation)
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http;
        var request = a with { Case = relation == "case" ? b.Case : a.Case, Status = relation == "status" ? b.Status : a.Status,
            General = relation == "general" ? b.General : a.General, Detail = relation == "detail" ? b.Detail : a.Detail };
        if (relation == "wrong-parent") request = request with { General = Id(await Created(ah, "/api/settings/competences", new { name = "Otra raíz" })) };
        Assert.Equal(relation == "case" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync($"/api/cases/{request.Case}/follow-ups", FollowUp(request))).StatusCode);
        var foreign = await Created(bh, $"/api/cases/{b.Case}/follow-ups", FollowUp(b));
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"/api/cases/{a.Case}/follow-ups/{Id(foreign)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"/api/cases/{b.Case}/follow-ups")).StatusCode);
    }

    [Fact]
    public async Task Tasks_ShouldCreateEditCompleteReopenCancelAndFilter()
    {
        var f = await Setup(); using var http = f.Http; var path = $"/api/cases/{f.Case}/tasks"; var due = DateTime.UtcNow.AddDays(1);
        var item = await Created(http, path, TaskBody(f.Member, due: due)); Assert.Equal(f.Member, item.GetProperty("assignedMembershipId").GetGuid());
        Assert.Equal(f.Member, item.GetProperty("createdByMembershipId").GetGuid());
        await Created(http, path, TaskBody(priority: "LOW"));
        (await http.PutAsJsonAsync($"{path}/{Id(item)}", TaskBody(f.Member, "IN_PROGRESS", due: due))).EnsureSuccessStatusCode();
        var filtered = await http.GetFromJsonAsync<JsonElement[]>($"{path}?status=IN_PROGRESS&priority=HIGH&assignedMembershipId={f.Member}&dueFrom={Uri.EscapeDataString(due.AddHours(-1).ToString("O"))}&dueTo={Uri.EscapeDataString(due.AddHours(1).ToString("O"))}"); Assert.Single(filtered!);
        var complete = await http.PatchAsJsonAsync($"{path}/{Id(item)}/status", new { status = "COMPLETED" }); complete.EnsureSuccessStatusCode();
        var completedAt = (await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completedAt").GetDateTime(); Assert.True(completedAt > DateTime.UtcNow.AddMinutes(-1));
        var repeated = await http.PatchAsJsonAsync($"{path}/{Id(item)}/status", new { status = "COMPLETED" }); Assert.Equal(completedAt, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completedAt").GetDateTime());
        var reopen = await http.PatchAsJsonAsync($"{path}/{Id(item)}/status", new { status = "PENDING" }); Assert.Equal(JsonValueKind.Null, (await reopen.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completedAt").ValueKind);
        await Patch(http, $"{path}/{Id(item)}/status", new { status = "CANCELLED" }); Assert.Single((await http.GetFromJsonAsync<JsonElement[]>($"{path}?status=CANCELLED"))!);
        Assert.Empty((await http.GetFromJsonAsync<JsonElement[]>($"{path}?priority=URGENT"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"{path}?status=INVALID")).StatusCode);
    }

    [Fact]
    public async Task Tasks_ShouldRejectForeignCaseTaskAndResponsibleAndInactiveResponsible()
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http; var ownPath = $"/api/cases/{a.Case}/tasks";
        Assert.Equal(HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync(ownPath, TaskBody(b.Member))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PostAsJsonAsync($"/api/cases/{b.Case}/tasks", TaskBody())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"/api/cases/{b.Case}/tasks")).StatusCode);
        var foreign = await Created(bh, $"/api/cases/{b.Case}/tasks", TaskBody());
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PutAsJsonAsync($"{ownPath}/{Id(foreign)}", TaskBody())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PatchAsJsonAsync($"{ownPath}/{Id(foreign)}/status", new { status = "COMPLETED" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Keep the caller active; add another inactive membership in this tenant.
        var inactive = new LegalManagement.Domain.Membership { OrganizationId = a.Org, UserId = (await db.Memberships.SingleAsync(x => x.Id == b.Member)).UserId, Status = "INACTIVE" };
        db.Memberships.Add(inactive); await db.SaveChangesAsync(); Assert.Equal(HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync(ownPath, TaskBody(inactive.Id))).StatusCode);
    }

    [Fact]
    public async Task Competences_ShouldIsolateEnforceHierarchyAndHideInactiveOperationalOptions()
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http;
        var list = await ah.GetFromJsonAsync<JsonElement[]>("/api/settings/competences"); Assert.Contains(list!, x => Id(x) == a.General); Assert.DoesNotContain(list!, x => Id(x) == b.General);
        Assert.Equal(a.General, list!.Single(x => Id(x) == a.Detail).GetProperty("parentId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync("/api/settings/competences", new { name = "Ajena", parentId = b.General })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync("/api/settings/competences", new { name = "Nieto", parentId = a.Detail })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PutAsJsonAsync($"/api/settings/competences/{b.General}", new { name = "Intruso" })).StatusCode);
        await Patch(ah, $"/api/settings/competences/{a.Detail}/status", new { isActive = false }); Assert.Empty((await ah.GetFromJsonAsync<JsonElement[]>($"/api/cases/competences?parentId={a.General}"))!);
        await Patch(ah, $"/api/settings/competences/{a.General}/status", new { isActive = false }); Assert.Empty((await ah.GetFromJsonAsync<JsonElement[]>("/api/cases/competences"))!);
        var editInactive = await ah.PutAsJsonAsync($"/api/settings/competences/{a.Detail}", new { name = "Detalle actualizado", parentId = a.General }); editInactive.EnsureSuccessStatusCode();
        Assert.Equal(a.General, (await editInactive.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parentId").GetGuid());
        Assert.Equal(2, (await ah.GetFromJsonAsync<JsonElement[]>("/api/settings/competences"))!.Length);
    }

    [Theory]
    [InlineData("LAWYER")]
    [InlineData("ASSISTANT")]
    [InlineData("READONLY")]
    public async Task Competences_ShouldRequireAdminWhilePreservingExistingMemberPolicy(string role)
    {
        var f = await Setup(); using var http = f.Http;
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var member = await db.Memberships.SingleAsync(x => x.Id == f.Member); member.RoleCode = role; await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsJsonAsync("/api/settings/competences", new { name = "Prohibida" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/settings/competences")).StatusCode);
        (await http.GetAsync("/api/cases/competences")).EnsureSuccessStatusCode();
        await Created(http, $"/api/cases/{f.Case}/tasks", TaskBody());
    }
}
