using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LegalManagement.Application;
using LegalManagement.Domain;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LegalManagement.IntegrationTests;

public class DocumentDashboardTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private record Fixture(HttpClient Http, Guid Org, Guid Member, Guid Case);
    private async Task<Fixture> Setup()
    {
        var http = factory.CreateClient(new() { HandleCookies = true }); http.DefaultRequestHeaders.Add("X-CSRF", "1");
        var email = $"sprint5-{Guid.NewGuid():N}@example.test";
        (await http.PostAsJsonAsync("/api/auth/register", new { firstName = "Ana", lastName = "Perez", email, password = "ValidPassw0rd!" })).EnsureSuccessStatusCode();
        var session = await (await http.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPassw0rd!" })).Content.ReadFromJsonAsync<SessionDto>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var org = await (await http.PostAsJsonAsync("/api/organizations", new { name = $"Firma {Guid.NewGuid():N}" })).Content.ReadFromJsonAsync<OrganizationDto>();
        http.DefaultRequestHeaders.Add("X-Organization-Id", org!.Id.ToString());
        var client = await (await http.PostAsJsonAsync("/api/clients", new { type = "PERSON", identificationType = "PASSPORT", identificationNumber = Guid.NewGuid().ToString("N"), firstName = "Ana", lastName = "Perez" })).Content.ReadFromJsonAsync<ClientDto>();
        var response = await http.PostAsJsonAsync("/api/cases", new { clientId = client!.Id, caseNumber = Guid.NewGuid().ToString("N"), title = "Caso documental", caseType = "CIVIL", status = "OPEN", priority = "MEDIUM", openedAt = DateTime.UtcNow }); response.EnsureSuccessStatusCode();
        var legalCase = await response.Content.ReadFromJsonAsync<CaseDto>();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new(http, org.Id, await db.Memberships.Where(x => x.OrganizationId == org.Id).Select(x => x.Id).SingleAsync(), legalCase!.Id);
    }
    private static string Base(Fixture f) => $"/api/cases/{f.Case}/documents";
    private static Task<HttpResponseMessage> Upload(Fixture f, string name = "prueba.pdf", string mime = "application/pdf", int size = 20, string type = "EVIDENCE")
    {
        var form = new MultipartFormDataContent(); var content = new ByteArrayContent(Enumerable.Repeat((byte)65, size).ToArray());
        content.Headers.ContentType = new(mime); form.Add(content, "file", name); form.Add(new StringContent(type), "documentType"); form.Add(new StringContent("Notas"), "notes");
        return f.Http.PostAsync(Base(f), form);
    }
    [Fact]
    public async Task DocumentLifecycle_ShouldKeepAuditAndPrivateHistoricalBinary()
    {
        var f = await Setup(); using var http = f.Http;
        var response = await Upload(f, "../../secret.pdf"); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("storageKey", json); Assert.DoesNotContain("storedFileName", json);
        var dto = (await response.Content.ReadFromJsonAsync<CaseDocumentDto>())!;
        Assert.Equal("secret.pdf", dto.OriginalFileName); Assert.Equal("Ana Perez", dto.UploadedBy); Assert.Equal("Notas", dto.Notes); Assert.Equal(20, dto.SizeBytes); Assert.Equal(".pdf", dto.Extension);
        Assert.Equal(dto.Id, Assert.Single((await http.GetFromJsonAsync<CaseDocumentDto[]>(Base(f)))!).Id);
        var download = await http.GetAsync($"{Base(f)}/{dto.Id}/download"); Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal(20, (await download.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync($"{Base(f)}/{dto.Id}")).StatusCode);
        Assert.Empty((await http.GetFromJsonAsync<CaseDocumentDto[]>(Base(f)))!);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"{Base(f)}/{dto.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync($"{Base(f)}/{dto.Id}")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.CaseDocuments.SingleAsync(x => x.Id == dto.Id);
        Assert.True(entity.IsDeleted); Assert.NotNull(entity.DeletedAt); Assert.Equal(f.Member, entity.UploadedByMembershipId); Assert.Equal(f.Member, entity.DeletedByMembershipId); Assert.Equal(f.Org, entity.OrganizationId);
        await using var stream = await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().OpenReadAsync(entity.StorageKey); Assert.Equal(20, stream.Length);
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Memberships.Where(x => x.Id == f.Member).ExecuteDeleteAsync());
    }
    [Theory]
    [InlineData("bad.exe", "application/octet-stream", 20, "OTHER", 400)]
    [InlineData("bad.pdf", "text/plain", 20, "OTHER", 400)]
    [InlineData("bad.pdf", "application/pdf", 0, "OTHER", 400)]
    [InlineData("bad.pdf", "application/pdf", 20, "INVALID", 400)]
    [InlineData("big.pdf", "application/pdf", 10485761, "OTHER", 413)]
    public async Task Documents_ShouldRejectInvalidUploads(string name, string mime, int size, string type, int status)
    { var f = await Setup(); using var http = f.Http; Assert.Equal(status, (int)(await Upload(f, name, mime, size, type)).StatusCode); Assert.Empty((await http.GetFromJsonAsync<CaseDocumentDto[]>(Base(f)))!); }
    [Fact]
    public async Task Documents_ShouldIsolateTenantAndCase()
    {
        var a = await Setup(); var b = await Setup(); using var ah = a.Http; using var bh = b.Http;
        var doc = (await (await Upload(b)).Content.ReadFromJsonAsync<CaseDocumentDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync(Base(b))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"{Base(b)}/{doc.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.DeleteAsync($"{Base(b)}/{doc.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ah.GetAsync($"{Base(a)}/{doc.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(a with { Case = b.Case })).StatusCode);
        ah.DefaultRequestHeaders.Remove("X-Organization-Id");ah.DefaultRequestHeaders.Add("X-Organization-Id",b.Org.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await ah.GetAsync(Base(b))).StatusCode);
    }
    [Fact]
    public async Task Dashboard_ShouldHandleEmptyDatasetAndInvalidZone()
    {
        var f = await Setup(); using var http = f.Http;
        var dto = (await http.GetFromJsonAsync<DashboardDto>("/api/dashboard?timeZone=America/Panama"))!;
        Assert.Equal(1, dto.ActiveCasesCount); Assert.Equal(0,dto.TodayTasksCount); Assert.Equal(0,dto.OverdueTasksCount); Assert.Equal(0,dto.TodayEventsCount);
        Assert.Empty(dto.UpcomingTasks);Assert.Empty(dto.UpcomingEvents);Assert.Empty(dto.StaleCases);Assert.Empty(dto.LatestFollowUps);
        Assert.Equal(7,dto.UpcomingDays);Assert.Equal(30,dto.StaleCaseDays);
        using(var scope=factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cases.Where(x=>x.Id==f.Case).ExecuteDeleteAsync();
        Assert.Equal(0,(await http.GetFromJsonAsync<DashboardDto>("/api/dashboard"))!.ActiveCasesCount);
        Assert.Equal(HttpStatusCode.BadRequest,(await http.GetAsync("/api/dashboard?timeZone=invalid-zone")).StatusCode);
    }
    [Fact]
    public async Task Dashboard_ShouldCountBoundariesUseSemanticStatusAndIsolateTenant()
    {
        var f=await Setup();var foreign=await Setup();using var http=f.Http;using var other=foreign.Http;
        var now=factory.Clock.GetUtcNow().UtcDateTime;var today=now.AddHours(-5).Date;var from=today.AddHours(5);var tomorrow=from.AddDays(1);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legalCase=await db.Cases.SingleAsync(x=>x.Id==f.Case);legalCase.CreatedAt=now.AddDays(-40);
            // Catalog semantics take precedence over the legacy status text.
            var status=await db.CaseStatuses.SingleAsync(x=>x.OrganizationId==f.Org&&x.Code=="OPEN");legalCase.CaseStatusId=status.Id;legalCase.Status="CLOSED";
            foreach(var date in new[]{from.AddTicks(-1),from,tomorrow.AddTicks(-1),tomorrow}) db.CaseTasks.Add(new(){OrganizationId=f.Org,CaseId=f.Case,Title="Boundary",DueAt=date,CreatedByMembershipId=f.Member});
            foreach(var taskStatus in new[]{"COMPLETED","CANCELLED"}) db.CaseTasks.Add(new(){OrganizationId=f.Org,CaseId=f.Case,Title="Excluded",DueAt=from,Status=taskStatus,CreatedByMembershipId=f.Member});
            db.CaseTasks.Add(new(){OrganizationId=foreign.Org,CaseId=foreign.Case,Title="Foreign",DueAt=from,CreatedByMembershipId=foreign.Member});
            db.CalendarEvents.AddRange(
                new(){OrganizationId=f.Org,Title="All day",AllDay=true,StartsAt=today.AddDays(-1),EndsAt=today,CreatedByMembershipId=f.Member},
                new(){OrganizationId=f.Org,Title="Timed",StartsAt=from,EndsAt=from.AddHours(1),CreatedByMembershipId=f.Member},
                new(){OrganizationId=f.Org,Title="Upper excluded",StartsAt=tomorrow,EndsAt=tomorrow.AddHours(1),CreatedByMembershipId=f.Member},
                new(){OrganizationId=f.Org,Title="Cancelled",StartsAt=from,EndsAt=tomorrow,Status="CANCELLED",CreatedByMembershipId=f.Member},
                new(){OrganizationId=foreign.Org,Title="Foreign",StartsAt=from,EndsAt=tomorrow,CreatedByMembershipId=foreign.Member});
            await db.SaveChangesAsync();
        }
        var dto=(await http.GetFromJsonAsync<DashboardDto>("/api/dashboard?timeZone=America/Panama"))!;
        Assert.Equal(1,dto.ActiveCasesCount);Assert.Equal(2,dto.TodayTasksCount);Assert.Equal(2,dto.TodayEventsCount);
        Assert.Equal(from<dto.GeneratedAt.UtcDateTime?2:1,dto.OverdueTasksCount);Assert.Equal(f.Case,Assert.Single(dto.StaleCases).Id);
        Assert.DoesNotContain(dto.UpcomingTasks,x=>x.Title=="Foreign");Assert.DoesNotContain(dto.UpcomingEvents,x=>x.Title is "Foreign" or "Cancelled");
    }
    [Fact]
    public async Task Dashboard_ShouldLimitListsAndOrderFollowUpsByOccurrence()
    {
        var f=await Setup();using var http=f.Http;var now=DateTime.UtcNow;
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var status=await db.CaseStatuses.FirstAsync(x=>x.OrganizationId==f.Org);
            var general=new LegalCompetence{OrganizationId=f.Org,Name="General"};var detail=new LegalCompetence{OrganizationId=f.Org,Name="Detail",ParentId=general.Id};db.AddRange(general,detail);
            for(var i=0;i<15;i++) {
                db.CaseTasks.Add(new(){OrganizationId=f.Org,CaseId=f.Case,Title=$"Task {i}",DueAt=now.AddHours(i+1),CreatedByMembershipId=f.Member});
                db.CalendarEvents.Add(new(){OrganizationId=f.Org,Title=$"Event {i}",StartsAt=now.AddHours(i+1),EndsAt=now.AddHours(i+2),CreatedByMembershipId=f.Member});
                db.CaseFollowUps.Add(new(){OrganizationId=f.Org,CaseId=f.Case,CaseStatusId=status.Id,CompetenceId=general.Id,CompetenceDetailId=detail.Id,Description=$"Follow {i}",OccurredAt=now.AddDays(-i),CreatedByMembershipId=f.Member});
            }
            var source=await db.Cases.SingleAsync(x=>x.Id==f.Case);
            for(var i=0;i<15;i++) db.Cases.Add(new(){OrganizationId=f.Org,ClientId=source.ClientId,CaseNumber=$"STALE-{i}",Title="Stale",CreatedAt=now.AddDays(-40-i),OpenedAt=now.AddDays(-40-i)});
            await db.SaveChangesAsync();
        }
        var dto=(await http.GetFromJsonAsync<DashboardDto>("/api/dashboard"))!;
        Assert.Equal(10,dto.UpcomingTasks.Count);Assert.Equal(10,dto.UpcomingEvents.Count);Assert.Equal(10,dto.LatestFollowUps.Count);Assert.Equal(10,dto.StaleCases.Count);
        Assert.Equal("Follow 0",dto.LatestFollowUps[0].Description);Assert.Equal("Task 0",dto.UpcomingTasks[0].Title);
        Assert.DoesNotContain(dto.StaleCases,x=>x.Id==f.Case);
    }

    [Fact]
    public async Task Documents_ShouldAuditDifferentDeleterAndPreserveExistingReadonlyPolicy()
    {
        var a=await Setup();var b=await Setup();using var ah=a.Http;using var bh=b.Http;
        var doc=(await (await Upload(a)).Content.ReadFromJsonAsync<CaseDocumentDto>())!;
        Guid deleter;
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user=(await db.Memberships.SingleAsync(x=>x.Id==b.Member)).UserId;
            var membership=new Membership{OrganizationId=a.Org,UserId=user,RoleCode="READONLY"};deleter=membership.Id;db.Memberships.Add(membership);await db.SaveChangesAsync();
        }
        bh.DefaultRequestHeaders.Remove("X-Organization-Id");bh.DefaultRequestHeaders.Add("X-Organization-Id",a.Org.ToString());
        Assert.Equal(HttpStatusCode.NoContent,(await bh.DeleteAsync($"{Base(a)}/{doc.Id}")).StatusCode);
        using(var scope=factory.Services.CreateScope()) {
            var entity=await scope.ServiceProvider.GetRequiredService<AppDbContext>().CaseDocuments.SingleAsync(x=>x.Id==doc.Id);
            Assert.Equal(a.Member,entity.UploadedByMembershipId);Assert.Equal(deleter,entity.DeletedByMembershipId);
        }
    }
    [Fact]
    public async Task Dashboard_ShouldRespectExclusiveUpcomingWindowAndStaleThreshold()
    {
        var f=await Setup();using var http=f.Http;var now=factory.Clock.GetUtcNow().UtcDateTime;
        var boundary=now.AddHours(-5).Date.AddDays(7).AddHours(5);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legalCase=await db.Cases.SingleAsync(x=>x.Id==f.Case);legalCase.CreatedAt=now.AddDays(-30);
            foreach(var due in new[]{now.AddTicks(-1),now,boundary.AddTicks(-1),boundary})
                db.CaseTasks.Add(new(){OrganizationId=f.Org,CaseId=f.Case,Title=due.ToString("O"),DueAt=due,CreatedByMembershipId=f.Member});
            foreach(var start in new[]{now,boundary})
                db.CalendarEvents.Add(new(){OrganizationId=f.Org,Title="Boundary",StartsAt=start,EndsAt=start.AddHours(1),CreatedByMembershipId=f.Member});
            await db.SaveChangesAsync();
        }
        var dto=(await http.GetFromJsonAsync<DashboardDto>("/api/dashboard?timeZone=America/Panama"))!;
        Assert.Equal(1,dto.OverdueTasksCount);Assert.Equal(2,dto.UpcomingTasks.Count);Assert.Equal(now,dto.UpcomingTasks[0].DueAt!.Value.UtcDateTime);
        Assert.Single(dto.UpcomingEvents);Assert.Empty(dto.StaleCases);
        using(var scope=factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legalCase=await db.Cases.SingleAsync(x=>x.Id==f.Case);legalCase.Status="CLOSED";legalCase.CaseStatusId=null;await db.SaveChangesAsync();
        }
        Assert.Equal(0,(await http.GetFromJsonAsync<DashboardDto>("/api/dashboard"))!.ActiveCasesCount);
    }
    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("C:\\secret.pdf")]
    [InlineData("/secret.pdf")]
    public async Task Storage_ShouldRejectTraversalKeys(string key)
    {
        var storage=new LocalDocumentStorage(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")),AppContext.BaseDirectory);
        await Assert.ThrowsAsync<ArgumentException>(()=>storage.OpenReadAsync(key));
    }
    [Fact]
    public void Storage_ShouldRejectPublicRoot()
    { Assert.Throws<InvalidOperationException>(()=>new LocalDocumentStorage("wwwroot/documents",AppContext.BaseDirectory)); }
}
