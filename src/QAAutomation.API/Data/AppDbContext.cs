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
    public DbSet<CallPipelineJob> CallPipelineJobs => Set<CallPipelineJob>();
    public DbSet<CallPipelineItem> CallPipelineItems => Set<CallPipelineItem>();
    public DbSet<SamplingPolicy> SamplingPolicies => Set<SamplingPolicy>();
    public DbSet<HumanReviewItem> HumanReviewItems => Set<HumanReviewItem>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<TrainingPlanItem> TrainingPlanItems => Set<TrainingPlanItem>();

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

        modelBuilder.Entity<CallPipelineJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne(e => e.Form)
                  .WithMany()
                  .HasForeignKey(e => e.FormId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Items)
                  .WithOne(i => i.Job)
                  .HasForeignKey(i => i.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CallPipelineItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne(e => e.EvaluationResult)
                  .WithMany()
                  .HasForeignKey(e => e.EvaluationResultId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SamplingPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SamplingMethod).HasMaxLength(20);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
        });

        modelBuilder.Entity<HumanReviewItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.SampledBy).HasMaxLength(200);
            entity.Property(e => e.AssignedTo).HasMaxLength(200);
            entity.Property(e => e.ReviewVerdict).HasMaxLength(20);
            entity.Property(e => e.ReviewedBy).HasMaxLength(200);
            entity.HasOne(e => e.EvaluationResult)
                  .WithMany()
                  .HasForeignKey(e => e.EvaluationResultId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SamplingPolicy)
                  .WithMany()
                  .HasForeignKey(e => e.SamplingPolicyId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(300);
            entity.Property(e => e.AgentName).HasMaxLength(200);
            entity.Property(e => e.AgentUsername).HasMaxLength(200);
            entity.Property(e => e.TrainerName).HasMaxLength(200);
            entity.Property(e => e.TrainerUsername).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.ClosedBy).HasMaxLength(200);
            entity.HasOne(e => e.EvaluationResult)
                  .WithMany()
                  .HasForeignKey(e => e.EvaluationResultId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.HumanReviewItem)
                  .WithMany()
                  .HasForeignKey(e => e.HumanReviewItemId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Items)
                  .WithOne(i => i.TrainingPlan)
                  .HasForeignKey(i => i.TrainingPlanId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrainingPlanItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetArea).HasMaxLength(200);
            entity.Property(e => e.ItemType).HasMaxLength(30);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.CompletedBy).HasMaxLength(200);
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

        // Seed sample evaluation (audit) records so the dashboard and Audit page show real data
        if (!await EvaluationResults.AnyAsync())
        {
            var form = await EvaluationForms
                .Include(f => f.Sections).ThenInclude(s => s.Fields)
                .FirstOrDefaultAsync(f => f.Name.Contains("Capital One"));

            if (form != null)
            {
                var fields = form.Sections.SelectMany(s => s.Fields).ToDictionary(f => f.Label);
                // Returns 0 when the form field is not found (guard below skips those records)
                int GetFieldId(string label) => fields.TryGetValue(label, out var ff) ? ff.Id : 0;

                // 5 realistic Capital One QA evaluations with varying performance
                var samples = new[]
                {
                    (agent: "Sarah Mitchell",  callRef: "COF-2025-00142", callDate: new DateTime(2025, 1, 14),  evaluatedBy: "admin", notes: "Excellent across all areas. Strong compliance adherence and outstanding customer rapport.",
                     scores: new[] { ("Professional Greeting",4.0),("Customer Identity Verification",1.0),("Brand Introduction",4.0),("First Call Resolution",5.0),("Product & Policy Knowledge",4.0),("Information Accuracy",5.0),("Problem-Solving Ability",4.0),("Verbal Clarity & Articulation",5.0),("Active Listening",4.0),("Empathy & Rapport Building",5.0),("Pace, Tone & Energy",4.0),("CFPB Regulatory Compliance",1.0),("Required Disclosures",1.0),("PCI Data Security",1.0),("Issue Summary & Confirmation",5.0),("Offer of Further Assistance",5.0),("Professional Sign-Off",4.0) }),
                    (agent: "James Kowalski",  callRef: "COF-2025-00215", callDate: new DateTime(2025, 1, 21),  evaluatedBy: "admin", notes: "Good performance overall. Missed required APR disclosure on the first attempt — corrected after customer prompt.",
                     scores: new[] { ("Professional Greeting",4.0),("Customer Identity Verification",1.0),("Brand Introduction",3.0),("First Call Resolution",4.0),("Product & Policy Knowledge",3.0),("Information Accuracy",4.0),("Problem-Solving Ability",3.0),("Verbal Clarity & Articulation",4.0),("Active Listening",3.0),("Empathy & Rapport Building",3.0),("Pace, Tone & Energy",3.0),("CFPB Regulatory Compliance",1.0),("Required Disclosures",0.0),("PCI Data Security",1.0),("Issue Summary & Confirmation",3.0),("Offer of Further Assistance",3.0),("Professional Sign-Off",4.0) }),
                    (agent: "Priya Nair",      callRef: "COF-2025-00318", callDate: new DateTime(2025, 2,  4),  evaluatedBy: "admin", notes: "Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout.",
                     scores: new[] { ("Professional Greeting",5.0),("Customer Identity Verification",1.0),("Brand Introduction",5.0),("First Call Resolution",5.0),("Product & Policy Knowledge",5.0),("Information Accuracy",5.0),("Problem-Solving Ability",5.0),("Verbal Clarity & Articulation",5.0),("Active Listening",5.0),("Empathy & Rapport Building",5.0),("Pace, Tone & Energy",5.0),("CFPB Regulatory Compliance",1.0),("Required Disclosures",1.0),("PCI Data Security",1.0),("Issue Summary & Confirmation",5.0),("Offer of Further Assistance",5.0),("Professional Sign-Off",5.0) }),
                    (agent: "Derek Thompson", callRef: "COF-2025-00401", callDate: new DateTime(2025, 2, 11),  evaluatedBy: "admin", notes: "Below standard. Failed to complete full CID verification and asked customer to repeat card number aloud — PCI violation. Immediate coaching required.",
                     scores: new[] { ("Professional Greeting",3.0),("Customer Identity Verification",0.0),("Brand Introduction",2.0),("First Call Resolution",2.0),("Product & Policy Knowledge",2.0),("Information Accuracy",3.0),("Problem-Solving Ability",2.0),("Verbal Clarity & Articulation",3.0),("Active Listening",2.0),("Empathy & Rapport Building",2.0),("Pace, Tone & Energy",2.0),("CFPB Regulatory Compliance",1.0),("Required Disclosures",1.0),("PCI Data Security",0.0),("Issue Summary & Confirmation",2.0),("Offer of Further Assistance",2.0),("Professional Sign-Off",3.0) }),
                    (agent: "Maria Gonzalez", callRef: "COF-2025-00487", callDate: new DateTime(2025, 2, 19),  evaluatedBy: "admin", notes: "Solid performance with good first-call resolution. Slight hesitation on product knowledge for balance transfer promotions.",
                     scores: new[] { ("Professional Greeting",5.0),("Customer Identity Verification",1.0),("Brand Introduction",4.0),("First Call Resolution",4.0),("Product & Policy Knowledge",3.0),("Information Accuracy",4.0),("Problem-Solving Ability",4.0),("Verbal Clarity & Articulation",4.0),("Active Listening",4.0),("Empathy & Rapport Building",4.0),("Pace, Tone & Energy",4.0),("CFPB Regulatory Compliance",1.0),("Required Disclosures",1.0),("PCI Data Security",1.0),("Issue Summary & Confirmation",4.0),("Offer of Further Assistance",4.0),("Professional Sign-Off",5.0) }),
                };

                // Each evaluation is recorded the next business day at 9 AM, staggered by index within the 7-hour window
                int index = 0;
                foreach (var s in samples)
                {
                    if (s.scores.Any(sc => GetFieldId(sc.Item1) == 0)) continue; // skip if form fields not found
                    var result = new EvaluationResult
                    {
                        FormId = form.Id,
                        EvaluatedBy = s.evaluatedBy,
                        EvaluatedAt = s.callDate.AddDays(1).AddHours(9 + index % 7),
                        Notes = s.notes,
                        AgentName = s.agent,
                        CallReference = s.callRef,
                        CallDate = s.callDate,
                        Scores = s.scores.Select(sc => new EvaluationScore
                        {
                            FieldId = GetFieldId(sc.Item1),
                            // Store the raw integer value as a string (e.g. "4", "1", "0")
                            Value = ((int)sc.Item2).ToString(),
                            NumericValue = sc.Item2
                        }).ToList()
                    };
                    EvaluationResults.Add(result);
                    index++;
                }
                await SaveChangesAsync();
            }
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
