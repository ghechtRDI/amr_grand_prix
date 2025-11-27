using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Models;

namespace AmrGrandPrix.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Race Results DbSets
    public DbSet<Race> Races { get; set; } = null!;
    public DbSet<Runner> Runners { get; set; } = null!;
    public DbSet<RaceResult> RaceResults { get; set; } = null!;
    public DbSet<GrandPrixPoints> GrandPrixPoints { get; set; } = null!;
    public DbSet<GrandPrixStanding> GrandPrixStandings { get; set; } = null!;
    public DbSet<UploadBatch> UploadBatches { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize Identity table names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
        });

        builder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        // Configure Race entity
        builder.Entity<Race>(entity =>
        {
            entity.HasKey(r => r.RaceId);
            entity.HasIndex(r => r.Year);
            entity.HasIndex(r => new { r.Year, r.IsGrandPrixRace });

            entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Date).IsRequired();
            entity.Property(r => r.Year).IsRequired();
        });

        // Configure Runner entity
        builder.Entity<Runner>(entity =>
        {
            entity.HasKey(r => r.RunnerId);
            entity.HasIndex(r => new { r.LastName, r.FirstName });
            entity.HasIndex(r => r.Email);

            entity.Property(r => r.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.LastName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Gender).IsRequired();
            entity.Ignore(r => r.FullName); // Computed property, not stored in DB
        });

        // Configure RaceResult entity
        builder.Entity<RaceResult>(entity =>
        {
            entity.HasKey(r => r.ResultId);
            entity.HasIndex(r => r.RaceId);
            entity.HasIndex(r => r.RunnerId);
            entity.HasIndex(r => r.UploadBatchId);
            entity.HasIndex(r => new { r.RaceId, r.RunnerId });

            entity.Property(r => r.Age).IsRequired();
            entity.Property(r => r.Gender).IsRequired();
            entity.Property(r => r.Status).IsRequired();

            // Configure relationships
            entity.HasOne(r => r.Race)
                .WithMany(race => race.Results)
                .HasForeignKey(r => r.RaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Runner)
                .WithMany(runner => runner.Results)
                .HasForeignKey(r => r.RunnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.UploadBatch)
                .WithMany(batch => batch.Results)
                .HasForeignKey(r => r.UploadBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure GrandPrixPoints entity
        builder.Entity<GrandPrixPoints>(entity =>
        {
            entity.HasKey(p => p.PointsId);
            entity.HasIndex(p => new { p.Year, p.Division });
            entity.HasIndex(p => p.RunnerId);
            entity.HasIndex(p => new { p.RunnerId, p.Year, p.Division });

            entity.Property(p => p.Year).IsRequired();
            entity.Property(p => p.Division).IsRequired();
            entity.Property(p => p.Points).IsRequired();

            // Configure relationships
            entity.HasOne(p => p.Runner)
                .WithMany(runner => runner.GrandPrixPoints)
                .HasForeignKey(p => p.RunnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Result)
                .WithMany(result => result.GrandPrixPoints)
                .HasForeignKey(p => p.ResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure GrandPrixStanding entity
        builder.Entity<GrandPrixStanding>(entity =>
        {
            entity.HasKey(s => s.StandingId);
            entity.HasIndex(s => new { s.Year, s.Division, s.Rank });
            entity.HasIndex(s => s.RunnerId);
            entity.HasIndex(s => new { s.RunnerId, s.Year });

            entity.Property(s => s.Year).IsRequired();
            entity.Property(s => s.Division).IsRequired();
            entity.Property(s => s.TotalPoints).IsRequired();
            entity.Property(s => s.Rank).IsRequired();

            // Configure relationship
            entity.HasOne(s => s.Runner)
                .WithMany(runner => runner.GrandPrixStandings)
                .HasForeignKey(s => s.RunnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure UploadBatch entity
        builder.Entity<UploadBatch>(entity =>
        {
            entity.HasKey(b => b.UploadBatchId);
            entity.HasIndex(b => b.RaceId);
            entity.HasIndex(b => b.UploadedAt);

            entity.Property(b => b.FileName).IsRequired().HasMaxLength(255);
            entity.Property(b => b.FileType).IsRequired();
            entity.Property(b => b.Status).IsRequired();
            entity.Property(b => b.UploadedBy).IsRequired();

            // Configure relationship
            entity.HasOne(b => b.Race)
                .WithMany(race => race.UploadBatches)
                .HasForeignKey(b => b.RaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
