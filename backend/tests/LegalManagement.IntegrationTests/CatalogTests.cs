using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LegalManagement.Application;

namespace LegalManagement.IntegrationTests;

public class CatalogTests(LegalManagementApiFactory factory) : IClassFixture<LegalManagementApiFactory>
{
    private const string Password = "ValidPassw0rd!";
    private async Task<(HttpClient Http, OrganizationDto Organization)> Setup(string prefix)
    {
        var http=factory.CreateClient(new(){HandleCookies=true}); http.DefaultRequestHeaders.Add("X-CSRF","1");
        var email=$"{prefix}-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created,(await http.PostAsJsonAsync("/api/auth/register",new{firstName="Test",lastName="Admin",email,password=Password})).StatusCode);
        var session=(await (await http.PostAsJsonAsync("/api/auth/login",new{email,password=Password})).Content.ReadFromJsonAsync<SessionDto>())!;
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.AccessToken);
        var org=(await (await http.PostAsJsonAsync("/api/organizations",new{name=$"Firma {Guid.NewGuid():N}"})).Content.ReadFromJsonAsync<OrganizationDto>())!;
        http.DefaultRequestHeaders.Add("X-Organization-Id",org.Id.ToString()); return(http,org);
    }
    private static object Payload(string endpoint,string name) => endpoint=="case-statuses"
        ? new {name,code=name.ToUpperInvariant().Replace(' ','_'),isOpen=true,isClosed=false,isInnocent=false,isGuilty=false,sortOrder=3}
        : new {name,sortOrder=3};
    private static async Task<Guid> Create(HttpClient http,string endpoint,string name)
    { var response=await http.PostAsJsonAsync($"/api/settings/{endpoint}",Payload(endpoint,name)); Assert.Equal(HttpStatusCode.Created,response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    [Theory]
    [InlineData("case-statuses","caseStatuses")]
    [InlineData("case-types","caseTypes")]
    [InlineData("courts","courts")]
    [InlineData("jurisdictions","jurisdictions")]
    public async Task Catalog_ShouldEnforceTenantCrudAndOperationalActivity(string endpoint,string optionsProperty)
    {
        var(a,_)=await Setup($"catalog-a-{endpoint}");var(b,_)=await Setup($"catalog-b-{endpoint}");using(a)using(b){
        var own=await Create(a,endpoint,$"Propio {Guid.NewGuid():N}");var foreign=await Create(b,endpoint,$"Ajeno {Guid.NewGuid():N}");
        var listed=await a.GetFromJsonAsync<JsonElement[]>($"/api/settings/{endpoint}");Assert.Contains(listed!,x=>x.GetProperty("id").GetGuid()==own);Assert.DoesNotContain(listed!,x=>x.GetProperty("id").GetGuid()==foreign);
        var updated=await a.PutAsJsonAsync($"/api/settings/{endpoint}/{own}",Payload(endpoint,"Actualizado"));updated.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound,(await a.PutAsJsonAsync($"/api/settings/{endpoint}/{foreign}",Payload(endpoint,"Intruso"))).StatusCode);
        var deactivate=new HttpRequestMessage(HttpMethod.Patch,$"/api/settings/{endpoint}/{own}/status"){Content=JsonContent.Create(new{isActive=false})};(await a.SendAsync(deactivate)).EnsureSuccessStatusCode();
        var options=await a.GetFromJsonAsync<JsonElement>("/api/cases/options");Assert.DoesNotContain(options.GetProperty(optionsProperty).EnumerateArray(),x=>x.GetProperty("id").GetGuid()==own); }
    }

    [Theory]
    [InlineData("case-statuses","caseStatusId")]
    [InlineData("case-types","caseTypeId")]
    [InlineData("courts","courtId")]
    [InlineData("jurisdictions","jurisdictionId")]
    public async Task Case_ShouldRejectForeignCatalogReference(string endpoint,string field)
    {
        var(a,_)=await Setup($"relation-a-{field}");var(b,_)=await Setup($"relation-b-{field}");using(a)using(b){var foreign=await Create(b,endpoint,$"Foreign {Guid.NewGuid():N}");
        var clientResponse=await a.PostAsJsonAsync("/api/clients",new{type="PERSON",identificationType="CEDULA",identificationNumber=Guid.NewGuid().ToString("N"),firstName="Ana",lastName="Perez"});
        var client=(await clientResponse.Content.ReadFromJsonAsync<ClientDto>())!;var body=new Dictionary<string,object?>{{"clientId",client.Id},{"caseNumber",Guid.NewGuid().ToString("N")},{"title","Caso"},{"caseType","CIVIL"},{"status","OPEN"},{"priority","MEDIUM"},{"openedAt",DateTime.UtcNow.Date},{field,foreign}};
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync("/api/cases",body)).StatusCode);}
    }

    [Fact]
    public async Task Case_HistoricalReferences_ShouldSurviveCatalogDeactivation()
    {
        var(http,_)=await Setup("historical-catalogs");using(http){var status=await Create(http,"case-statuses","Historico abierto");var type=await Create(http,"case-types","Historico tipo");var court=await Create(http,"courts","Historico tribunal");var jurisdiction=await Create(http,"jurisdictions","Historico jurisdiccion");
        var client=(await (await http.PostAsJsonAsync("/api/clients",new{type="PERSON",identificationType="PASSPORT",identificationNumber=Guid.NewGuid().ToString("N"),firstName="Ana",lastName="Perez"})).Content.ReadFromJsonAsync<ClientDto>())!;
        var createdResponse=await http.PostAsJsonAsync("/api/cases",new{clientId=client.Id,caseNumber=Guid.NewGuid().ToString("N"),title="Historico",caseStatusId=status,caseTypeId=type,courtId=court,jurisdictionId=jurisdiction,priority="MEDIUM",openedAt=DateTime.UtcNow.Date});createdResponse.EnsureSuccessStatusCode();var created=(await createdResponse.Content.ReadFromJsonAsync<CaseDto>())!;
        foreach(var pair in new[]{("case-statuses",status),("case-types",type),("courts",court),("jurisdictions",jurisdiction)}){var request=new HttpRequestMessage(HttpMethod.Patch,$"/api/settings/{pair.Item1}/{pair.Item2}/status"){Content=JsonContent.Create(new{isActive=false})};(await http.SendAsync(request)).EnsureSuccessStatusCode();}
        var historical=await http.GetFromJsonAsync<CaseDto>($"/api/cases/{created.Id}");Assert.Equal(status,historical!.CaseStatusId);Assert.Equal(type,historical.CaseTypeId);Assert.Equal(court,historical.CourtId);Assert.Equal(jurisdiction,historical.JurisdictionId);}
    }
}
