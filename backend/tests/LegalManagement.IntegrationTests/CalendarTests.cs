using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class CalendarTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private record Fixture(HttpClient Http, Guid Org, Guid Member, Guid Case);
    private async Task<Fixture> Setup()
    {
        var http = factory.CreateClient(new() { HandleCookies = true }); http.DefaultRequestHeaders.Add("X-CSRF", "1");
        var email = $"calendar-{Guid.NewGuid():N}@example.test";
        (await http.PostAsJsonAsync("/api/auth/register", new { firstName = "Ana", lastName = "Perez", email, password = "ValidPassw0rd!" })).EnsureSuccessStatusCode();
        var session = await (await http.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPassw0rd!" })).Content.ReadFromJsonAsync<SessionDto>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var org = await (await http.PostAsJsonAsync("/api/organizations", new { name = $"Firma {Guid.NewGuid():N}" })).Content.ReadFromJsonAsync<OrganizationDto>();
        http.DefaultRequestHeaders.Add("X-Organization-Id", org!.Id.ToString());
        var client = await (await http.PostAsJsonAsync("/api/clients", new { type = "PERSON", identificationType = "PASSPORT", identificationNumber = Guid.NewGuid().ToString("N"), firstName = "Ana", lastName = "Perez" })).Content.ReadFromJsonAsync<ClientDto>();
        var response = await http.PostAsJsonAsync("/api/cases", new { clientId = client!.Id, caseNumber = Guid.NewGuid().ToString("N"), title = "Caso de Agenda", caseType = "CIVIL", status = "OPEN", priority = "MEDIUM", openedAt = DateTime.UtcNow }); response.EnsureSuccessStatusCode();
        var legalCase = await response.Content.ReadFromJsonAsync<CaseDto>();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new(http, org.Id, await db.Memberships.Where(x => x.OrganizationId == org.Id).Select(x => x.Id).SingleAsync(), legalCase!.Id);
    }
    private static CalendarEventRequest Body(Fixture f) => new("Audiencia", f.Case, "HEARING", "SCHEDULED", f.Member,
        new DateTimeOffset(2026,10,5,9,0,0,TimeSpan.FromHours(-5)), new DateTimeOffset(2026,10,5,10,0,0,TimeSpan.FromHours(-5)),
        false, "Juzgado Primero", "https://example.test/meeting", "Documentación judicial");
    private static async Task<CalendarEventDto> Create(HttpClient http, CalendarEventRequest body)
    { var response = await http.PostAsJsonAsync("/api/calendar/events", body); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!; }
    private static async Task<IReadOnlyList<CalendarEventDto>> List(HttpClient http, string query = "") => (await http.GetFromJsonAsync<CalendarEventDto[]>($"/api/calendar/events{query}"))!;
    private static string Enc(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

    [Fact]
    public async Task Event_ShouldCreateGetEditAndPreserveCreatorWithoutCreatingTasks()
    {
        var f = await Setup(); using var http = f.Http; var item = await Create(http, Body(f));
        Assert.Equal(f.Case, item.CaseId); Assert.Equal(f.Member, item.AssignedMembershipId); Assert.Equal(f.Member, item.CreatedByMembershipId);
        Assert.Equal("Ana Perez", item.AssignedName); Assert.Contains("Caso de Agenda", item.CaseName!);
        var get = await http.GetFromJsonAsync<CalendarEventDto>($"/api/calendar/events/{item.Id}"); Assert.Equal(item, get);
        var update = await http.PutAsJsonAsync($"/api/calendar/events/{item.Id}", Body(f) with { Title = "Audiencia actualizada", EventType = "MEETING", Description = "Notas nuevas" }); update.EnsureSuccessStatusCode();
        var saved = (await update.Content.ReadFromJsonAsync<CalendarEventDto>())!; Assert.Equal("Audiencia actualizada", saved.Title); Assert.Equal("MEETING", saved.EventType); Assert.Equal(item.CreatedAt, saved.CreatedAt); Assert.Equal(item.CreatedByMembershipId, saved.CreatedByMembershipId);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.CalendarEvents.SingleAsync(x => x.Id == item.Id); Assert.Equal(f.Org, entity.OrganizationId);
        Assert.Equal(new DateTime(2026,10,5,14,0,0), entity.StartsAt); Assert.Equal(TimeSpan.Zero, saved.StartsAt.Offset);
        Assert.False(await db.CaseTasks.AnyAsync(x => x.OrganizationId == f.Org));
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELLED")]
    public async Task Event_ShouldCompleteOrCancelWithoutDeleting(string status)
    {
        var f = await Setup(); using var http = f.Http; var item = await Create(http, Body(f));
        var response = await http.PatchAsJsonAsync($"/api/calendar/events/{item.Id}/status", new { status }); response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<CalendarEventDto>())!; Assert.Equal(status, saved.Status); Assert.Equal(item.StartsAt, saved.StartsAt);
        Assert.Equal(item.Id, Assert.Single(await List(http, $"?status={status}")).Id);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await http.DeleteAsync($"/api/calendar/events/{item.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("case")]
    [InlineData("member")]
    [InlineData("inactive-member")]
    public async Task Event_ShouldRejectInvalidTenantReferencesOnCreateAndUpdate(string relation)
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http; var body = Body(a);
        if (relation == "case") body = body with { CaseId = b.Case };
        if (relation == "member") body = body with { AssignedMembershipId = b.Member };
        if (relation == "inactive-member") {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inactive = new Membership { OrganizationId = a.Org, UserId = (await db.Memberships.SingleAsync(x => x.Id == b.Member)).UserId, Status = "INACTIVE" };
            db.Memberships.Add(inactive); await db.SaveChangesAsync(); body = body with { AssignedMembershipId = inactive.Id };
            Assert.DoesNotContain((await ah.GetFromJsonAsync<CalendarOptionsDto>("/api/calendar/options"))!.ResponsibleMemberships, x => x.Id == inactive.Id);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await ah.PostAsJsonAsync("/api/calendar/events", body)).StatusCode);
        var item = await Create(ah, Body(a)); Assert.Equal(HttpStatusCode.BadRequest, (await ah.PutAsJsonAsync($"/api/calendar/events/{item.Id}", body)).StatusCode);
    }

    [Fact]
    public async Task EventAndOptions_ShouldIsolateTenantsForReadsAndMutations()
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http;
        var own = await Create(ah, Body(a)); var foreign = await Create(bh, Body(b));
        Assert.Equal(own.Id, Assert.Single(await List(ah)).Id); Assert.DoesNotContain(await List(ah), x => x.Id == foreign.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"/api/calendar/events/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PutAsJsonAsync($"/api/calendar/events/{foreign.Id}", Body(a))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.PatchAsJsonAsync($"/api/calendar/events/{foreign.Id}/status", new { status = "CANCELLED" })).StatusCode);
        var options = (await ah.GetFromJsonAsync<CalendarOptionsDto>("/api/calendar/options"))!;
        Assert.Contains(options.Cases, x => x.Id == a.Case); Assert.DoesNotContain(options.Cases, x => x.Id == b.Case); Assert.DoesNotContain(options.ResponsibleMemberships, x => x.Id == b.Member);
        Assert.Empty(await List(ah, $"?assignedMembershipId={b.Member}")); Assert.Equal(HttpStatusCode.BadRequest, (await ah.GetAsync($"/api/calendar/events?caseId={b.Case}")).StatusCode);
    }

    [Theory]
    [InlineData("inverted")]
    [InlineData("missing-start")]
    [InlineData("missing-end")]
    [InlineData("type")]
    [InlineData("status")]
    [InlineData("url")]
    public async Task Event_ShouldValidateDatesAndDomainValues(string invalid)
    {
        var f = await Setup(); using var http = f.Http; var original = Body(f);
        var body = invalid switch {
            "inverted" => original with { EndsAt = original.StartsAt.AddMinutes(-1) },
            "missing-start" => original with { StartsAt = default },
            "missing-end" => original with { EndsAt = default },
            "type" => original with { EventType = "INVALID" },
            "status" => original with { Status = "INVALID" },
            _ => original with { MeetingUrl = "javascript:alert(1)" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/calendar/events", body)).StatusCode);
        var item = await Create(http, original); Assert.Equal(HttpStatusCode.BadRequest, (await http.PatchAsJsonAsync($"/api/calendar/events/{item.Id}/status", new { status = "INVALID" })).StatusCode);
    }

    [Fact]
    public async Task Event_ShouldFilterByOverlapCaseMemberTypeAndStatus()
    {
        var f = await Setup(); using var http = f.Http; var body = Body(f); var item = await Create(http, body);
        await Create(http, body with { CaseId = null, AssignedMembershipId = null, EventType = "OTHER", Status = "CANCELLED", StartsAt = body.StartsAt.AddDays(3), EndsAt = body.EndsAt.AddDays(3) });
        foreach (var query in new[] { $"?caseId={f.Case}", $"?assignedMembershipId={f.Member}", "?eventType=HEARING", "?status=SCHEDULED",
            $"?from={Enc(body.StartsAt.AddMinutes(30))}&to={Enc(body.EndsAt.AddMinutes(30))}",
            $"?from={Enc(body.StartsAt)}&to={Enc(body.EndsAt)}&caseId={f.Case}&assignedMembershipId={f.Member}&eventType=HEARING&status=SCHEDULED" })
            Assert.Equal(item.Id, Assert.Single(await List(http, query)).Id);
        Assert.Empty(await List(http, $"?from={Enc(body.EndsAt.AddMinutes(1))}&to={Enc(body.EndsAt.AddHours(1))}"));
        Assert.Empty(await List(http, $"?from={Enc(body.EndsAt)}&to={Enc(body.EndsAt.AddHours(1))}"));
        Assert.Empty(await List(http, $"?to={Enc(body.StartsAt)}"));
        foreach (var query in new[] { $"?from={Enc(body.EndsAt)}&to={Enc(body.StartsAt)}", "?eventType=INVALID", "?status=INVALID" })
            Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"/api/calendar/events{query}")).StatusCode);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(-12)]
    [InlineData(0)]
    public async Task AllDay_ShouldPreserveCivilDatesAtTimezoneExtremesAndFilterBoundaries(int offsetHours)
    {
        var f = await Setup(); using var http = f.Http; var start = new DateTimeOffset(2026,10,5,0,0,0,TimeSpan.FromHours(offsetHours));
        var item = await Create(http, Body(f) with { AllDay = true, StartsAt = start, EndsAt = start.AddDays(2) });
        Assert.Equal(new DateTimeOffset(2026,10,5,0,0,0,TimeSpan.Zero), item.StartsAt); Assert.Equal(new DateTimeOffset(2026,10,7,0,0,0,TimeSpan.Zero), item.EndsAt);
        Assert.Equal(item.Id, Assert.Single(await List(http, $"?from={Enc(start.AddDays(2))}&to={Enc(start.AddDays(3))}")).Id);
        Assert.Empty(await List(http, $"?from={Enc(start.AddDays(3))}&to={Enc(start.AddDays(4))}"));
        Assert.Empty(await List(http, $"?from={Enc(start.AddDays(-1))}&to={Enc(start)}"));
        Assert.Equal(item.Id, Assert.Single(await List(http, $"?from={Enc(start)}&to={Enc(start.AddHours(12))}")).Id);
        var edited = await http.PutAsJsonAsync($"/api/calendar/events/{item.Id}", Body(f) with { AllDay = true, StartsAt = start, EndsAt = start }); edited.EnsureSuccessStatusCode();
        Assert.Equal(start.Date, (await edited.Content.ReadFromJsonAsync<CalendarEventDto>())!.EndsAt.Date);
    }

    [Theory]
    [InlineData("LAWYER")]
    [InlineData("ASSISTANT")]
    [InlineData("READONLY")]
    public async Task Event_ShouldReuseExistingMemberPolicy(string role)
    {
        var f = await Setup(); using var http = f.Http;
        using(var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var member = await db.Memberships.SingleAsync(x => x.Id == f.Member); member.RoleCode = role; await db.SaveChangesAsync(); }
        await Create(http, Body(f));
        Assert.Single(await List(http));
        http.DefaultRequestHeaders.Remove("X-Organization-Id"); Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/calendar/events")).StatusCode);
    }

    [Fact]
    public async Task Event_ShouldAllowEqualTimesAndProtectHistoricalReferences()
    {
        var f = await Setup(); using var http = f.Http; var body = Body(f); var item = await Create(http, body with { EndsAt = body.StartsAt });
        Assert.Equal(item.StartsAt, item.EndsAt);
        Assert.Equal(item.Id, Assert.Single(await List(http, $"?from={Enc(body.StartsAt)}&to={Enc(body.StartsAt.AddHours(1))}")).Id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Cases.Remove(await db.Cases.SingleAsync(x => x.Id == f.Case)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var options = await http.GetFromJsonAsync<CalendarOptionsDto>("/api/calendar/options"); Assert.Contains(options!.Cases, x => x.Id == f.Case);
    }
}
