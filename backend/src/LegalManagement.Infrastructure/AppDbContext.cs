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
    }
}
