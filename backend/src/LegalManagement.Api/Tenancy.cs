using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LegalManagement.Api;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class TenantRequiredAttribute : Attribute;

public sealed class TenantContext : ITenantContext
{
    public Guid OrganizationId { get; private set; }
    public string UserId { get; private set; } = "";
    public string RoleCode { get; private set; } = "";
    public bool IsResolved { get; private set; }
    public void Resolve(Guid id, string user, string role) => (OrganizationId, UserId, RoleCode, IsResolved) = (id, user, role, true);
}

public sealed class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, AppDbContext db, TenantContext tenant)
    {
        if (http.GetEndpoint()?.Metadata.GetMetadata<TenantRequiredAttribute>() is null || http.User.Identity?.IsAuthenticated != true)
        { await next(http); return; }
        var userId = http.User.FindFirst("sub")!.Value;
        var header = http.Request.Headers["X-Organization-Id"];
        if (header.Count != 1 || !Guid.TryParse(header, out var id))
            throw new ApiException(400, "Se requiere X-Organization-Id válido.");
        if (!Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var routeId) || routeId != id)
            throw new ApiException(403, "La organización de la ruta no coincide con la organización activa.");
        var membership = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId &&
            m.OrganizationId == id && m.Status == "ACTIVE" && m.Organization.Status == "ACTIVE");
        if (membership is null)
        {
            logger.LogWarning("TenantAccessDenied UserId={UserId} OrganizationId={OrganizationId}", userId, id);
            throw new ApiException(403, "No tienes acceso a esta organización.");
        }
        tenant.Resolve(id, userId, membership.RoleCode);
        await next(http);
    }
}

public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttributes(typeof(TenantRequiredAttribute), true).Length > 0)
            operation.Parameters.Add(new() { Name = "X-Organization-Id", In = ParameterLocation.Header, Required = true,
                Description = "Debe coincidir con la ruta y tener membresía ACTIVE.", Schema = new() { Type = "string", Format = "uuid" } });
        if (context.ApiDescription.RelativePath?.StartsWith("api/auth/") == true)
            operation.Parameters.Add(new() { Name = "X-CSRF", In = ParameterLocation.Header, Required = true,
                Schema = new() { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString("1") } });
    }
}

public class ApiException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public sealed class ApiErrors(RequestDelegate next, ILogger<ApiErrors> logger)
{
    public async Task InvokeAsync(HttpContext http)
    {
        try { await next(http); }
        catch (Exception ex)
        {
            var (status, title) = ex switch {
                ApiException a => (a.Status, a.Message),
                DbUpdateConcurrencyException => (409, "La operación ya fue procesada. Actualiza e intenta de nuevo."),
                DbUpdateException => (409, "No se pudo guardar: el registro ya existe o cambió."),
                _ => (500, "Ocurrió un error inesperado.")
            };
            // Exception messages, paths and SQL parameters may contain tokens or personal data.
            if (status == 500) logger.LogError("UnhandledError Type={Type} TraceId={TraceId}", ex.GetType().Name, http.TraceIdentifier);
            http.Response.StatusCode = status;
            await http.Response.WriteAsJsonAsync(new { title, status, traceId = http.TraceIdentifier });
        }
    }
}
