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
    public DbSet<AiConfig> AiConfigs => Set<AiConfig>();
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Lob> Lobs => Set<Lob>();
    public DbSet<UserProjectAccess> UserProjectAccesses => Set<UserProjectAccess>();

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
            entity.Property(e => e.AgentName).HasMaxLength(200);
            entity.Property(e => e.CallReference).HasMaxLength(100);
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

        modelBuilder.Entity<AiConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LlmProvider).HasMaxLength(50);
            entity.Property(e => e.SentimentProvider).HasMaxLength(50);
        });

        modelBuilder.Entity<KnowledgeSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ConnectorType).IsRequired().HasMaxLength(50);
            entity.HasMany(e => e.Documents)
                  .WithOne(d => d.Source)
                  .HasForeignKey(d => d.SourceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.Lobs)
                  .WithOne(l => l.Project)
                  .HasForeignKey(l => l.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.UserAccess)
                  .WithOne(a => a.Project)
                  .HasForeignKey(a => a.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Lob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(e => e.EvaluationForms)
                  .WithOne(f => f.Lob)
                  .HasForeignKey(f => f.LobId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserProjectAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ProjectId }).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
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

        if (!await Parameters.AnyAsync())
        {
            // ── Default Project & LOB ─────────────────────────────────────────
            var project = new Project
            {
                Name = "Capital One",
                Description = "Capital One Financial Corporation",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Projects.Add(project);
            await SaveChangesAsync();

            var lob = new Lob
            {
                ProjectId = project.Id,
                Name = "Customer Support Call",
                Description = "Customer support call centre quality evaluation",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Lobs.Add(lob);
            await SaveChangesAsync();

            // Grant admin user access to default project
            var admin = await AppUsers.FirstOrDefaultAsync(u => u.Username == "admin");
            if (admin != null)
            {
                UserProjectAccesses.Add(new UserProjectAccess { UserId = admin.Id, ProjectId = project.Id, GrantedAt = DateTime.UtcNow });
                await SaveChangesAsync();
            }

            // Rating Criteria
            var qaScore = new RatingCriteria
            {
                Name = "QA Score (1-5)",
                Description = "Standard quality score from 1 (Unacceptable) to 5 (Outstanding)",
                MinScore = 1,
                MaxScore = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ProjectId = project.Id,
                Levels = new List<RatingLevel>
                {
                    new() { Score = 1, Label = "Unacceptable", Description = "Critical failure affecting customer or compliance", Color = "#dc3545" },
                    new() { Score = 2, Label = "Needs Improvement", Description = "Below standard performance", Color = "#fd7e14" },
                    new() { Score = 3, Label = "Meets Standard", Description = "Satisfactory performance meeting expectations", Color = "#ffc107" },
                    new() { Score = 4, Label = "Exceeds Standard", Description = "Above average performance", Color = "#20c997" },
                    new() { Score = 5, Label = "Outstanding", Description = "Exemplary performance, exceeded all expectations", Color = "#198754" }
                }
            };
            var complianceCriteria = new RatingCriteria
            {
                Name = "Compliance (Pass/Fail)",
                Description = "Binary compliance check - failure is auto-fail",
                MinScore = 0,
                MaxScore = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ProjectId = project.Id,
                Levels = new List<RatingLevel>
                {
                    new() { Score = 0, Label = "FAIL", Description = "Non-compliant — requires immediate coaching", Color = "#dc3545" },
                    new() { Score = 1, Label = "PASS", Description = "Compliant with policy and regulation", Color = "#198754" }
                }
            };
            RatingCriteria.AddRange(qaScore, complianceCriteria);
            await SaveChangesAsync();

            // Parameters
            var paramDefs = new[]
            {
                // Call Opening
                ("Professional Greeting", "Agent uses approved greeting script with brand name, own name, and offer to help", "Call Opening", 1.0),
                ("Customer Identity Verification", "Completes full CID verification per PCI/security policy before discussing account", "Call Opening", 2.0),
                ("Brand Introduction", "Sets the right tone and properly introduces Capital One services", "Call Opening", 1.0),
                // Issue Resolution
                ("First Call Resolution", "Resolves customer issue completely without need for callback or transfer", "Issue Resolution", 3.0),
                ("Product & Policy Knowledge", "Demonstrates accurate knowledge of Capital One credit card products, rates, and policies", "Issue Resolution", 2.0),
                ("Information Accuracy", "All information provided to customer is accurate and up to date", "Issue Resolution", 2.5),
                ("Problem-Solving Ability", "Effectively identifies root cause and provides appropriate resolution or alternatives", "Issue Resolution", 2.0),
                // Communication Skills
                ("Verbal Clarity & Articulation", "Speaks clearly, avoids jargon, adjusts language to customer's level", "Communication Skills", 1.5),
                ("Active Listening", "Demonstrates understanding, does not interrupt, confirms understanding before proceeding", "Communication Skills", 1.5),
                ("Empathy & Rapport Building", "Acknowledges customer emotions, personalizes the interaction, builds trust", "Communication Skills", 2.0),
                ("Pace, Tone & Energy", "Maintains professional tone throughout, appropriate pace, positive energy", "Communication Skills", 1.0),
                // Compliance & Procedures
                ("CFPB Regulatory Compliance", "Adheres to all CFPB regulations including fair lending, UDAAP, and debt collection rules", "Compliance & Procedures", 5.0),
                ("Required Disclosures", "Provides all mandatory disclosures (APR, fees, payment terms) as required by Reg Z", "Compliance & Procedures", 3.0),
                ("PCI Data Security", "Does not capture, repeat, or store sensitive payment card data in violation of PCI DSS", "Compliance & Procedures", 5.0),
                // Call Closing
                ("Issue Summary & Confirmation", "Summarizes resolution and confirms customer satisfaction before closing", "Call Closing", 1.5),
                ("Offer of Further Assistance", "Proactively asks if customer needs anything else before ending the call", "Call Closing", 1.0),
                ("Professional Sign-Off", "Uses approved closing script, thanks customer, and ends call professionally", "Call Closing", 1.0),
            };

            foreach (var (name, desc, cat, weight) in paramDefs)
            {
                Parameters.Add(new Parameter { Name = name, Description = desc, Category = cat, DefaultWeight = weight, IsActive = true, CreatedAt = DateTime.UtcNow, ProjectId = project.Id });
            }
            await SaveChangesAsync();

            // Helper to look up saved IDs
            var allParams = await Parameters.ToListAsync();
            int Pid(string name) => allParams.First(p => p.Name == name).Id;
            int qaScoreId = qaScore.Id;
            int complianceId = complianceCriteria.Id;

            // ParameterClubs
            var clubDefs = new[]
            {
                ("Call Opening", "Initial call handling — greeting, verification, and brand introduction",
                    new[] {
                        ("Professional Greeting", qaScoreId),
                        ("Customer Identity Verification", complianceId),
                        ("Brand Introduction", qaScoreId)
                    }),
                ("Issue Resolution", "Effectiveness in understanding and resolving the customer's credit card issue",
                    new[] {
                        ("First Call Resolution", qaScoreId),
                        ("Product & Policy Knowledge", qaScoreId),
                        ("Information Accuracy", qaScoreId),
                        ("Problem-Solving Ability", qaScoreId)
                    }),
                ("Communication Skills", "Quality of verbal communication and relationship building",
                    new[] {
                        ("Verbal Clarity & Articulation", qaScoreId),
                        ("Active Listening", qaScoreId),
                        ("Empathy & Rapport Building", qaScoreId),
                        ("Pace, Tone & Energy", qaScoreId)
                    }),
                ("Compliance & Procedures", "Adherence to regulatory and internal compliance requirements — violations are auto-fail",
                    new[] {
                        ("CFPB Regulatory Compliance", complianceId),
                        ("Required Disclosures", complianceId),
                        ("PCI Data Security", complianceId)
                    }),
                ("Call Closing", "Professional and thorough call conclusion",
                    new[] {
                        ("Issue Summary & Confirmation", qaScoreId),
                        ("Offer of Further Assistance", qaScoreId),
                        ("Professional Sign-Off", qaScoreId)
                    }),
            };

            foreach (var (clubName, clubDesc, items) in clubDefs)
            {
                var club = new ParameterClub
                {
                    Name = clubName,
                    Description = clubDesc,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ProjectId = project.Id,
                    Items = items.Select((item, idx) => new ParameterClubItem
                    {
                        ParameterId = Pid(item.Item1),
                        RatingCriteriaId = item.Item2,
                        Order = idx
                    }).ToList()
                };
                ParameterClubs.Add(club);
            }
            await SaveChangesAsync();

            // EvaluationForm (linked to default LOB)
            var form = new EvaluationForm
            {
                Name = "Capital One — Credit Card Customer Support QA Form",
                Description = "Quality evaluation form for Capital One credit card customer support interactions. Covers call handling, issue resolution, communication, compliance, and closing.",
                IsActive = true,
                LobId = lob.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Sections = new List<FormSection>
                {
                    new() {
                        Title = "Call Opening", Order = 0,
                        Fields = new List<FormField>
                        {
                            new() { Label = "Professional Greeting", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 0 },
                            new() { Label = "Customer Identity Verification", FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 1 },
                            new() { Label = "Brand Introduction", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 2 },
                        }
                    },
                    new() {
                        Title = "Issue Resolution", Order = 1,
                        Fields = new List<FormField>
                        {
                            new() { Label = "First Call Resolution", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 0 },
                            new() { Label = "Product & Policy Knowledge", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 1 },
                            new() { Label = "Information Accuracy", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = true, Order = 2 },
                            new() { Label = "Problem-Solving Ability", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 3 },
                        }
                    },
                    new() {
                        Title = "Communication Skills", Order = 2,
                        Fields = new List<FormField>
                        {
                            new() { Label = "Verbal Clarity & Articulation", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 0 },
                            new() { Label = "Active Listening", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 1 },
                            new() { Label = "Empathy & Rapport Building", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 2 },
                            new() { Label = "Pace, Tone & Energy", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 3 },
                        }
                    },
                    new() {
                        Title = "Compliance & Procedures", Order = 3,
                        Fields = new List<FormField>
                        {
                            new() { Label = "CFPB Regulatory Compliance", FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 0 },
                            new() { Label = "Required Disclosures", FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 1 },
                            new() { Label = "PCI Data Security", FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 2 },
                        }
                    },
                    new() {
                        Title = "Call Closing", Order = 4,
                        Fields = new List<FormField>
                        {
                            new() { Label = "Issue Summary & Confirmation", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 0 },
                            new() { Label = "Offer of Further Assistance", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 1 },
                            new() { Label = "Professional Sign-Off", FieldType = FieldType.Rating, MaxRating = 5, IsRequired = false, Order = 2 },
                        }
                    },
                }
            };
            EvaluationForms.Add(form);
            await SaveChangesAsync();
        }

        // Seed default AI config if not present
        if (!await AiConfigs.AnyAsync())
        {
            AiConfigs.Add(new AiConfig
            {
                Id = 1,
                LlmProvider = "AzureOpenAI",
                LlmEndpoint = "",
                LlmApiKey = "",
                LlmDeployment = "gpt-4o",
                LlmTemperature = 0.1f,
                SentimentProvider = "AzureOpenAI",
                LanguageEndpoint = "",
                LanguageApiKey = "",
                RagTopK = 3,
                UpdatedAt = DateTime.UtcNow
            });
            await SaveChangesAsync();
        }
    }
}
