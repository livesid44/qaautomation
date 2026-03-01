using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Models;
using System.Security.Cryptography;
using System.Text;

namespace QAAutomation.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EvaluationForm> EvaluationForms => Set<EvaluationForm>();
    public DbSet<FormSection> FormSections => Set<FormSection>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();
    public DbSet<EvaluationScore> EvaluationScores => Set<EvaluationScore>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Parameter> Parameters => Set<Parameter>();
    public DbSet<ParameterClub> ParameterClubs => Set<ParameterClub>();
    public DbSet<ParameterClubItem> ParameterClubItems => Set<ParameterClubItem>();
    public DbSet<RatingCriteria> RatingCriteria => Set<RatingCriteria>();
    public DbSet<RatingLevel> RatingLevels => Set<RatingLevel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EvaluationForm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.Sections)
                  .WithOne(s => s.Form)
                  .HasForeignKey(s => s.FormId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormSection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.Fields)
                  .WithOne(f => f.Section)
                  .HasForeignKey(f => f.SectionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormField>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<EvaluationResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EvaluatedBy).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Form)
                  .WithMany()
                  .HasForeignKey(e => e.FormId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Scores)
                  .WithOne(s => s.Result)
                  .HasForeignKey(s => s.ResultId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvaluationScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Field)
                  .WithMany()
                  .HasForeignKey(e => e.FieldId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<Parameter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<ParameterClub>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Club)
                  .HasForeignKey(i => i.ClubId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParameterClubItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Parameter)
                  .WithMany()
                  .HasForeignKey(e => e.ParameterId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RatingCriteria)
                  .WithMany()
                  .HasForeignKey(e => e.RatingCriteriaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RatingCriteria>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.Levels)
                  .WithOne(l => l.Criteria)
                  .HasForeignKey(l => l.CriteriaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RatingLevel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(20);
        });
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    public async Task SeedAsync()
    {
        if (!await AppUsers.AnyAsync())
        {
            AppUsers.Add(new AppUser
            {
                Username = "admin",
                PasswordHash = HashPassword("Admin@123"),
                Email = "admin@qaautomation.local",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await SaveChangesAsync();
        }
    }
}
