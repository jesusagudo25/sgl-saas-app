using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LegalManagement.IntegrationTests;

public sealed class LegalManagementApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"LegalManagementTests_{Guid.NewGuid():N}";
    private bool databaseDeleted;
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "LegalManagement.Tests",
                ["Jwt:Audience"] = "LegalManagement.Tests.Client",
                ["Jwt:SigningKey"] = "integration-tests-only-signing-key-32-bytes-minimum",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["FrontendUrl"] = "http://localhost",
                ["Cors:AllowedOrigins:0"] = "http://localhost"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !databaseDeleted)
        {
            databaseDeleted = true;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}
