using System.Text;
using System.Threading.RateLimiting;
using LegalManagement.Api;
using LegalManagement.Application;
using LegalManagement.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);

builder.Configuration.AddEnvironmentVariables();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32 || string.IsNullOrWhiteSpace(jwt.Issuer) ||
    string.IsNullOrWhiteSpace(jwt.Audience) || jwt.AccessTokenMinutes is < 1 or > 60 || jwt.RefreshTokenDays is < 1 or > 30)
    throw new InvalidOperationException("Configure Jwt:Issuer, Audience, SigningKey (32+ bytes), AccessTokenMinutes (1–60), RefreshTokenDays (1–30).");
    
var frontend = builder.Configuration["FrontendUrl"];
if (!Uri.TryCreate(frontend, UriKind.Absolute, out var frontendUri) ||
    (frontendUri.Scheme != "https" && !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))))
    throw new InvalidOperationException("Configure una FrontendUrl HTTPS válida (HTTP permitido solo en desarrollo/pruebas).");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 10;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();

builder.Services.AddScoped<AccessTokens>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<DevelopmentEmailSender>();
    builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevelopmentEmailSender>());
}
else throw new InvalidOperationException("Antes de producción, configure una implementación real de IEmailSender. El proveedor simulado está deshabilitado.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(20),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(context.Principal?.FindFirst("sub")?.Value ?? "");
            if (user is null || !user.IsActive || user.SecurityStamp != context.Principal?.FindFirst("sst")?.Value)
                context.Fail("Sesión inválida.");
        }
    };
});

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("OrganizationMember", p => p.RequireAuthenticatedUser().RequireAssertion(c =>
        c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ITenantContext>().IsResolved));
    o.AddPolicy("OrganizationAdmin", p => p.RequireAuthenticatedUser().RequireAssertion(c =>
        c.Resource is HttpContext http && http.RequestServices.GetRequiredService<ITenantContext>() is { IsResolved: true, RoleCode: "ADMIN" }));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [frontend!])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
        {
            PermitLimit = builder.Environment.IsEnvironment("Testing") ? 10000 : 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "Legal Management · Sprint 0", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
    o.OperationFilter<TenantHeaderOperationFilter>();
});
var app = builder.Build();
app.UseMiddleware<ApiErrors>();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-store";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    // Custom header plus origin validation protects cookie-mutating auth operations.
    if (ctx.Request.Method == "POST" && ctx.Request.Path.StartsWithSegments("/api/auth"))
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [frontend!];
        if (ctx.Request.Headers["X-CSRF"] != "1" || (origin.Length > 0 && !origins.Contains(origin)))
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { title = "Origen de solicitud no permitido.", status = 403 });
            return;
        }
    }
    await next();
});
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment())
    app.MapGet("/api/development/emails", (HttpContext ctx, DevelopmentEmailSender sender) =>
        ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)
            ? Results.Ok(sender.Messages) : Results.NotFound()).ExcludeFromDescription();
app.Run();
public partial class Program { }
