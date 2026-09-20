using LegalManagement.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LegalManagement.Infrastructure;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStatus> CaseStatuses => Set<CaseStatus>();
    public DbSet<CaseType> CaseTypes => Set<CaseType>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Jurisdiction> Jurisdictions => Set<Jurisdiction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ApplicationUser>(e => {
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
        });
        b.Entity<Organization>(e => {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Membership>(e => {
            e.Property(x => x.RoleCode).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Status });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Invitation>(e => {
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.RoleCode).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20).IsConcurrencyToken();
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Email, x.Status });
            e.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique().HasFilter("[Status] = 'PENDING'");
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RefreshToken>(e => {
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.CreatedByIp).HasMaxLength(45);
            e.Property(x => x.RevokedByIp).HasMaxLength(45);
            e.Property(x => x.RevokedAt).IsConcurrencyToken();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Client>(e => {
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(240).IsRequired();
            e.Property(x => x.IdentificationType).HasMaxLength(40).IsRequired();
            e.Property(x => x.IdentificationNumber).HasMaxLength(80).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.SecondaryPhone).HasMaxLength(40);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(120);
            e.Property(x => x.LastName).HasMaxLength(120);
            e.Property(x => x.LegalName).HasMaxLength(240);
            e.Property(x => x.TradeName).HasMaxLength(240);
            e.Property(x => x.ContactPerson).HasMaxLength(240);
            e.HasIndex(x => new { x.OrganizationId, x.IdentificationNumber }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Status, x.Type });
            e.HasIndex(x => new { x.OrganizationId, x.DisplayName });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Case>(e => {
            e.Property(x => x.CaseNumber).HasMaxLength(80).IsRequired();
            e.Property(x => x.Title).HasMaxLength(240).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.CaseType).HasMaxLength(120).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            e.Property(x => x.Court).HasMaxLength(240);
            e.Property(x => x.Jurisdiction).HasMaxLength(240);
            e.Property(x => x.Counterparty).HasMaxLength(240);
            e.Property(x => x.OpposingCounsel).HasMaxLength(240);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => new { x.OrganizationId, x.CaseNumber }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Status, x.Priority });
            e.HasIndex(x => new { x.OrganizationId, x.ClientId });
            e.HasIndex(x => new { x.OrganizationId, x.ResponsibleMembershipId });
            e.HasIndex(x => new { x.OrganizationId, x.CaseStatusId });
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ResponsibleMembership).WithMany().HasForeignKey(x => x.ResponsibleMembershipId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CaseStatus).WithMany().HasForeignKey(x => x.CaseStatusId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CaseTypeReference).WithMany().HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CourtReference).WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.JurisdictionReference).WithMany().HasForeignKey(x => x.JurisdictionId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureCatalog<CaseType>(b);
        ConfigureCatalog<Court>(b);
        ConfigureCatalog<Jurisdiction>(b);
        b.Entity<CaseStatus>(e => {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCatalog<T>(ModelBuilder b) where T : OrganizationCatalog
    {
        b.Entity<T>(e => {
            e.Property(x => x.Name).HasMaxLength(240).IsRequired();
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
