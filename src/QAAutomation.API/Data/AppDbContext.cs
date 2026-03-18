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
            // Locate the first evaluation form in the system (the default seed form) by
            // finding the form associated with the first project's LOB, rather than relying
            // on the project's display name.
            var defaultProject = await Projects.OrderBy(p => p.Id).FirstOrDefaultAsync();
            var form = defaultProject == null ? null : await EvaluationForms
                .Include(f => f.Sections).ThenInclude(s => s.Fields)
                .Include(f => f.Lob)
                .FirstOrDefaultAsync(f => f.Lob != null && f.Lob.ProjectId == defaultProject.Id);

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

        // Seed Sampling Policies
        if (!await SamplingPolicies.AnyAsync())
        {
            var project = await Projects.FirstOrDefaultAsync();
            SamplingPolicies.AddRange(
                new SamplingPolicy
                {
                    Name = "10% Random Sampling",
                    Description = "Sample 10% of all completed evaluations for human review",
                    ProjectId = project?.Id,
                    SamplingMethod = "Percentage",
                    SampleValue = 10f,
                    IsActive = true,
                    CreatedBy = "admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SamplingPolicy
                {
                    Name = "Compliance Risk — All Failing Calls",
                    Description = "100% review for calls with compliance-related failures",
                    ProjectId = project?.Id,
                    CallTypeFilter = "Compliance",
                    SamplingMethod = "Percentage",
                    SampleValue = 100f,
                    IsActive = true,
                    CreatedBy = "admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SamplingPolicy
                {
                    Name = "New Agents — 5 Calls per Week",
                    Description = "Review the first 5 calls per week for newly onboarded agents",
                    ProjectId = project?.Id,
                    SamplingMethod = "Count",
                    SampleValue = 5f,
                    IsActive = true,
                    CreatedBy = "admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            await SaveChangesAsync();
        }

        // Seed Human Review Items (linked to seeded evaluation results)
        if (!await HumanReviewItems.AnyAsync())
        {
            var results = await EvaluationResults.ToListAsync();
            var policy = await SamplingPolicies.FirstOrDefaultAsync();
            if (results.Count >= 4 && policy != null)
            {
                HumanReviewItems.AddRange(
                    new HumanReviewItem
                    {
                        EvaluationResultId = results[0].Id,
                        SamplingPolicyId = policy.Id,
                        SampledAt = results[0].EvaluatedAt.AddHours(1),
                        SampledBy = "system",
                        AssignedTo = "admin",
                        Status = "Reviewed",
                        ReviewerComment = "Agree with the AI scoring. Agent demonstrated excellent product knowledge and compliance.",
                        ReviewVerdict = "Agree",
                        ReviewedBy = "admin",
                        ReviewedAt = results[0].EvaluatedAt.AddDays(1)
                    },
                    new HumanReviewItem
                    {
                        EvaluationResultId = results[1].Id,
                        SamplingPolicyId = policy.Id,
                        SampledAt = results[1].EvaluatedAt.AddHours(1),
                        SampledBy = "system",
                        AssignedTo = "admin",
                        Status = "Reviewed",
                        ReviewerComment = "Partially agree — the Required Disclosures failure was borderline; agent did mention APR but not in the required format.",
                        ReviewVerdict = "Partial",
                        ReviewedBy = "admin",
                        ReviewedAt = results[1].EvaluatedAt.AddDays(1)
                    },
                    new HumanReviewItem
                    {
                        EvaluationResultId = results[3].Id,
                        SamplingPolicyId = policy.Id,
                        SampledAt = results[3].EvaluatedAt.AddHours(2),
                        SampledBy = "system",
                        AssignedTo = "admin",
                        Status = "Pending",
                        ReviewerComment = null,
                        ReviewVerdict = null,
                        ReviewedBy = null,
                        ReviewedAt = null
                    }
                );
                await SaveChangesAsync();
            }
        }

        // Seed Training Plans
        if (!await TrainingPlans.AnyAsync())
        {
            var project = await Projects.FirstOrDefaultAsync();
            var humanReviewItems = await HumanReviewItems.ToListAsync();
            var evalResults = await EvaluationResults.ToListAsync();

            // Plan 1 — PCI Violation for Derek Thompson
            var plan1 = new TrainingPlan
            {
                Title = "PCI DSS Compliance Remediation — Derek Thompson",
                Description = "Immediate coaching required following PCI DSS violation (COF-2025-00401). Agent asked customer to repeat card number verbally and failed full CID verification.",
                AgentName = "Derek Thompson",
                TrainerName = "admin",
                Status = "Active",
                DueDate = new DateTime(2025, 3, 10),
                ProjectId = project?.Id,
                EvaluationResultId = evalResults.Count > 3 ? evalResults[3].Id : null,
                CreatedBy = "admin",
                CreatedAt = new DateTime(2025, 2, 12),
                UpdatedAt = new DateTime(2025, 2, 12),
                Items = new List<TrainingPlanItem>
                {
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Observation", Content = "Agent asked the customer to repeat their full card number aloud, violating PCI DSS data security requirements.", Status = "Pending", Order = 0 },
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Observation", Content = "Customer Identity Verification (CID) process was not completed before account details were discussed.", Status = "Pending", Order = 1 },
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Recommendation", Content = "Complete the PCI DSS e-learning module (30 min) and pass the assessment with a minimum score of 80%.", Status = "Pending", Order = 2 },
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Recommendation", Content = "Complete a live role-play session with the trainer covering CID verification and sensitive data handling.", Status = "Pending", Order = 3 },
                    new() { TargetArea = "Call Opening", ItemType = "Recommendation", Content = "Review the CID verification checklist and practise the verification script until it becomes second nature.", Status = "Pending", Order = 4 },
                }
            };

            // Plan 2 — Disclosure gap for James Kowalski
            var plan2 = new TrainingPlan
            {
                Title = "Required Disclosures Coaching — James Kowalski",
                Description = "APR and fee disclosure was not provided in the required format on call COF-2025-00215. Coaching to reinforce Reg Z disclosure requirements.",
                AgentName = "James Kowalski",
                TrainerName = "admin",
                Status = "InProgress",
                DueDate = new DateTime(2025, 3, 5),
                ProjectId = project?.Id,
                EvaluationResultId = evalResults.Count > 1 ? evalResults[1].Id : null,
                CreatedBy = "admin",
                CreatedAt = new DateTime(2025, 1, 22),
                UpdatedAt = new DateTime(2025, 2, 1),
                Items = new List<TrainingPlanItem>
                {
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Observation", Content = "Required APR and fee disclosures were mentioned but not delivered in the mandatory scripted format as required by Reg Z.", Status = "Done", Order = 0, CompletedBy = "admin", CompletedAt = new DateTime(2025, 2, 1), CompletionNotes = "Agent reviewed the Reg Z disclosure script and confirmed understanding." },
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Recommendation", Content = "Review the Reg Z required disclosure scripts for all Capital One credit card product categories.", Status = "InProgress", Order = 1 },
                    new() { TargetArea = "Compliance & Procedures", ItemType = "Recommendation", Content = "Conduct two supervised calls where the trainer monitors disclosure delivery in real time.", Status = "Pending", Order = 2 },
                }
            };

            TrainingPlans.AddRange(plan1, plan2);
            await SaveChangesAsync();
        }

        // Seed Call Pipeline Jobs
        if (!await CallPipelineJobs.AnyAsync())
        {
            var project = await Projects.FirstOrDefaultAsync();
            var form = await EvaluationForms.FirstOrDefaultAsync();
            if (project != null && form != null)
            {
                var job1 = new CallPipelineJob
                {
                    Name = "Capital One — Weekly Batch Jan W3 2025",
                    SourceType = "BatchUrl",
                    FormId = form.Id,
                    ProjectId = project.Id,
                    Status = "Completed",
                    CreatedAt = new DateTime(2025, 1, 20),
                    StartedAt = new DateTime(2025, 1, 20, 9, 0, 0),
                    CompletedAt = new DateTime(2025, 1, 20, 9, 18, 0),
                    CreatedBy = "admin",
                    Items = new List<CallPipelineItem>
                    {
                        new() { SourceReference = "https://recordings.capitalone.internal/calls/COF-2025-00142.mp3", AgentName = "Sarah Mitchell", CallReference = "COF-2025-00142", CallDate = new DateTime(2025, 1, 14), Status = "Completed", CreatedAt = new DateTime(2025, 1, 20), ProcessedAt = new DateTime(2025, 1, 20, 9, 5, 0), ScorePercent = 91.2, AiReasoning = "Excellent performance across all evaluation areas. Strong compliance adherence and outstanding customer rapport." },
                        new() { SourceReference = "https://recordings.capitalone.internal/calls/COF-2025-00215.mp3", AgentName = "James Kowalski", CallReference = "COF-2025-00215", CallDate = new DateTime(2025, 1, 21), Status = "Completed", CreatedAt = new DateTime(2025, 1, 20), ProcessedAt = new DateTime(2025, 1, 20, 9, 10, 0), ScorePercent = 73.5, AiReasoning = "Good overall performance. Missed required APR disclosure on the first attempt — corrected after customer prompt." },
                        new() { SourceReference = "https://recordings.capitalone.internal/calls/COF-2025-00155.mp3", AgentName = "Maria Gonzalez", CallReference = "COF-2025-00155", CallDate = new DateTime(2025, 1, 16), Status = "Failed", CreatedAt = new DateTime(2025, 1, 20), ProcessedAt = new DateTime(2025, 1, 20, 9, 15, 0), ErrorMessage = "Audio quality too poor to transcribe reliably (SNR < 10 dB)." },
                    }
                };
                var job2 = new CallPipelineJob
                {
                    Name = "Capital One — Weekly Batch Feb W1 2025",
                    SourceType = "BatchUrl",
                    FormId = form.Id,
                    ProjectId = project.Id,
                    Status = "Completed",
                    CreatedAt = new DateTime(2025, 2, 3),
                    StartedAt = new DateTime(2025, 2, 3, 9, 0, 0),
                    CompletedAt = new DateTime(2025, 2, 3, 9, 22, 0),
                    CreatedBy = "admin",
                    Items = new List<CallPipelineItem>
                    {
                        new() { SourceReference = "https://recordings.capitalone.internal/calls/COF-2025-00318.mp3", AgentName = "Priya Nair", CallReference = "COF-2025-00318", CallDate = new DateTime(2025, 2, 4), Status = "Completed", CreatedAt = new DateTime(2025, 2, 3), ProcessedAt = new DateTime(2025, 2, 3, 9, 8, 0), ScorePercent = 98.5, AiReasoning = "Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout." },
                        new() { SourceReference = "https://recordings.capitalone.internal/calls/COF-2025-00401.mp3", AgentName = "Derek Thompson", CallReference = "COF-2025-00401", CallDate = new DateTime(2025, 2, 11), Status = "Completed", CreatedAt = new DateTime(2025, 2, 3), ProcessedAt = new DateTime(2025, 2, 3, 9, 16, 0), ScorePercent = 38.2, AiReasoning = "Below standard. Failed CID verification and PCI DSS violation detected. Immediate coaching required." },
                    }
                };
                CallPipelineJobs.AddRange(job1, job2);
                await SaveChangesAsync();
            }
        }

        // Seed Knowledge Base
        if (!await KnowledgeSources.AnyAsync())
        {
            // Use the first project by creation order (the default seed project) rather than
            // relying on the project's display name, so this works even if the name changes.
            var project = await Projects.OrderBy(p => p.Id).FirstOrDefaultAsync();
            var source = new KnowledgeSource
            {
                Name = "Capital One QA Policy Documents",
                ConnectorType = "ManualUpload",
                Description = "Internal QA policies, compliance guidelines, and evaluation rubrics for Capital One customer support",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow,
                ProjectId = project?.Id,
                Documents = new List<KnowledgeDocument>
                {
                    new()
                    {
                        Title = "PCI DSS Agent Guidelines v4.0",
                        FileName = "PCI_DSS_Agent_Guidelines_v4.pdf",
                        Content = "PCI DSS Scope for Call Centre Agents\n\nAgents MUST NOT:\n- Ask customers to read aloud full card numbers\n- Write down, store, or repeat Primary Account Numbers (PAN) in any form\n- Record sensitive authentication data after authorisation\n\nAgents MUST:\n- Complete CID verification before accessing any account information\n- Use masked card numbers (last 4 digits only) when confirming identity\n- Immediately terminate calls where customers attempt to provide full card numbers verbally and request they use the secure IVR channel\n\nViolations are classified as Critical and result in immediate remediation and mandatory retraining.",
                        Tags = "PCI,Compliance,Security,CID",
                        ContentSizeBytes = 1240,
                        UploadedAt = new DateTime(2025, 1, 1)
                    },
                    new()
                    {
                        Title = "Reg Z Required Disclosures Script",
                        FileName = "RegZ_Disclosure_Script_2025.pdf",
                        Content = "Regulation Z — Required Verbal Disclosures for Credit Card Calls\n\nWhen discussing APR or fees, agents must deliver the following scripted disclosure:\n\n\"Just to let you know, [Product Name] has a variable APR of [X]% for purchases, [Y]% for cash advances, and a minimum interest charge of $[Z]. Late fees are up to $[amount]. Please refer to your Cardmember Agreement for full terms.\"\n\nThis disclosure is mandatory whenever:\n- Opening a new account\n- Discussing promotional rates\n- Responding to balance transfer enquiries\n- Any conversation involving credit terms or fees\n\nFailure to deliver this disclosure in full is a Reg Z compliance violation.",
                        Tags = "Compliance,RegZ,Disclosures,Fees",
                        ContentSizeBytes = 980,
                        UploadedAt = new DateTime(2025, 1, 1)
                    },
                    new()
                    {
                        Title = "QA Evaluation Rubric — Communication Skills",
                        FileName = "QA_Rubric_Communication_2025.pdf",
                        Content = "Communication Skills Evaluation Rubric\n\nScore 5 (Outstanding): Agent communicates with exceptional clarity, warmth, and professionalism. Uses the customer's name naturally, matches their communication style, and uses precise, jargon-free language throughout.\n\nScore 4 (Exceeds Standard): Clear and professional communication with minor inconsistencies. Customer-centric language used. Active listening demonstrated.\n\nScore 3 (Meets Standard): Acceptable communication. Occasional lapses in clarity or empathy but no significant impact on customer experience.\n\nScore 2 (Needs Improvement): Unclear communication, interrupts customer, or uses inappropriate tone. Customer experience negatively impacted.\n\nScore 1 (Unacceptable): Rude, dismissive, or seriously unclear communication. Immediate coaching required.",
                        Tags = "Communication,Rubric,Scoring,Training",
                        ContentSizeBytes = 870,
                        UploadedAt = new DateTime(2025, 1, 15)
                    }
                }
            };
            KnowledgeSources.Add(source);
            await SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds the YouTube project with LOB "CSO" and an evaluation form based on the
    /// YouTube IQA (Internal Quality Assurance) framework. Safe to call on every startup
    /// — exits immediately if the "Youtube" project already exists.
    /// </summary>
    public async Task SeedYouTubeAsync()
    {
        // ── Project, LOB, parameters and evaluation form ───────────────────────
        // Guard: only run this block once, on first startup. All of the objects
        // below are created together; if the project exists they are already there.
        // ytProject is declared here so the KB backfill block below can reuse it
        // without a second name-based lookup.
        Project? ytProject = null;
        if (!await Projects.AnyAsync(p => p.Name == "Youtube"))
        {
        // ── Project & LOB ──────────────────────────────────────────────────────
        ytProject = new Project
        {
            Name = "Youtube",
            Description = "YouTube Creator Support Operations — Internal Quality Assurance",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Projects.Add(ytProject);
        await SaveChangesAsync();

        var csoLob = new Lob
        {
            ProjectId = ytProject.Id,
            Name = "CSO",
            Description = "Creator Support Operations line of business",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Lobs.Add(csoLob);
        await SaveChangesAsync();

        // Grant admin user access to the YouTube project
        var adminUser = await AppUsers.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser != null && !await UserProjectAccesses.AnyAsync(a => a.UserId == adminUser.Id && a.ProjectId == ytProject.Id))
        {
            UserProjectAccesses.Add(new UserProjectAccess { UserId = adminUser.Id, ProjectId = ytProject.Id, GrantedAt = DateTime.UtcNow });
            await SaveChangesAsync();
        }

        // ── Rating Criteria (shared Pass/Fail for all YouTube competencies) ────
        var ytPassFail = new RatingCriteria
        {
            Name = "YouTube IQA — Pass/Fail",
            Description = "Non-compensatory pass/fail used across all YouTube IQA competencies. Failure in any mandatory competency results in auto-fail for that category.",
            MinScore = 0,
            MaxScore = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ProjectId = ytProject.Id,
            Levels = new List<RatingLevel>
            {
                new() { Score = 0, Label = "FAIL", Description = "Competency not met — triggers category auto-fail", Color = "#dc3545" },
                new() { Score = 1, Label = "PASS", Description = "Competency met or not applicable (Yes/NA)", Color = "#198754" }
            }
        };
        RatingCriteria.Add(ytPassFail);
        await SaveChangesAsync();

        // ── Parameters (one per IQA competency) ───────────────────────────────
        // Creator Critical — Effectiveness
        var ytParamDefs = new[]
        {
            ("Accuracy",                   "Did the creator receive an accurate and complete solution for all the informed issues?",                                                                  "Creator Critical — Effectiveness", 1.0),
            ("Tailoring",                  "Were the issues or expectations of the creator met with the right level of personalisation?",                                                             "Creator Critical — Effectiveness", 1.0),
            ("Obviation & Next Steps",     "Has the creator been equipped with relevant obviation opportunities and next steps?",                                                                     "Creator Critical — Effectiveness", 1.0),
            // Creator Critical — Effort
            ("Responsiveness",             "Have we set and/or kept expectations with regards to timely and proactive follow-up communications?",                                                     "Creator Critical — Effort",        1.0),
            ("Internal Coordination",      "Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?",                                          "Creator Critical — Effort",        1.0),
            ("Workflows Adherence",        "Did we minimise creator effort by following correct workflows?",                                                                                         "Creator Critical — Effort",        1.0),
            ("Creator Feedback",           "Was the creator reassured that their feedback was captured and addressed?",                                                                              "Creator Critical — Effort",        1.0),
            ("CSAT Survey",                "Was the creator appropriately asked to provide feedback through a CSAT survey?",                                                                         "Creator Critical — Effort",        1.0),
            // Creator Critical — Engagement
            ("Clarity",                    "Has the creator received clear communication through the use of correct language and effective questioning?",                                             "Creator Critical — Engagement",    1.0),
            ("Empathy",                    "Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?",                                       "Creator Critical — Engagement",    1.0),
            ("Tone",                       "Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?",                          "Creator Critical — Engagement",    1.0),
            // Business Critical
            ("Due Diligence",              "Did the agent complete all required due-diligence steps before responding or escalating?",                                                               "Business Critical",                1.0),
            ("Issue Tagging",              "Was the case correctly tagged / categorised using Neo Categorization?",                                                                                  "Business Critical",                1.0),
            // Compliance Critical
            ("Authentication",             "Did the agent follow the correct authentication process before discussing account or creator details?",                                                  "Compliance Critical",              1.0),
            ("Keep YouTube Safe",          "Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?",                                        "Compliance Critical",              1.0),
            ("Policy",                     "Did the agent correctly apply and communicate YouTube policies relevant to the creator's issue?",                                                        "Compliance Critical",              1.0),
        };

        foreach (var (name, desc, cat, weight) in ytParamDefs)
        {
            Parameters.Add(new Parameter
            {
                Name = name,
                Description = desc,
                Category = cat,
                DefaultWeight = weight,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ProjectId = ytProject.Id
            });
        }
        await SaveChangesAsync();

        var ytAllParams = await Parameters.Where(p => p.ProjectId == ytProject.Id).ToListAsync();
        int GetYtParamId(string name) => ytAllParams.First(p => p.Name == name).Id;
        int pfId = ytPassFail.Id;

        // ── Parameter Clubs (one per IQA category / CX driver) ────────────────
        var ytClubDefs = new[]
        {
            ("Creator Critical – Effectiveness",
             "Measures whether the creator received an accurate, tailored, and complete solution including relevant next steps.",
             new[] { "Accuracy", "Tailoring", "Obviation & Next Steps" }),

            ("Creator Critical – Effort",
             "Measures how much effort was required for the creator to reach resolution, covering responsiveness, coordination, workflows, and feedback loops.",
             new[] { "Responsiveness", "Internal Coordination", "Workflows Adherence", "Creator Feedback", "CSAT Survey" }),

            ("Creator Critical – Engagement",
             "Measures how the creator felt during the interaction in terms of communication clarity, empathy, and professional tone.",
             new[] { "Clarity", "Empathy", "Tone" }),

            ("Business Critical",
             "Non-compensatory business-critical checks: due diligence and correct issue tagging.",
             new[] { "Due Diligence", "Issue Tagging" }),

            ("Compliance Critical",
             "Non-compensatory compliance checks: authentication, trust & safety, and policy adherence.",
             new[] { "Authentication", "Keep YouTube Safe", "Policy" }),
        };

        foreach (var (clubName, clubDesc, paramNames) in ytClubDefs)
        {
            var club = new ParameterClub
            {
                Name = clubName,
                Description = clubDesc,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ProjectId = ytProject.Id,
                Items = paramNames.Select((pName, idx) => new ParameterClubItem
                {
                    ParameterId = GetYtParamId(pName),
                    RatingCriteriaId = pfId,
                    Order = idx
                }).ToList()
            };
            ParameterClubs.Add(club);
        }
        await SaveChangesAsync();

        // ── Evaluation Form ────────────────────────────────────────────────────
        var ytForm = new EvaluationForm
        {
            Name = "YouTube CSO IQA Evaluation Form",
            Description = "Internal Quality Assurance evaluation form for YouTube Creator Support Operations. Based on the YouTube CSO QA Framework covering Effectiveness, Effort, Engagement, Business Critical, and Compliance Critical competencies.",
            IsActive = true,
            LobId = csoLob.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Sections = new List<FormSection>
            {
                new()
                {
                    Title = "Creator Critical – Effectiveness",
                    Description = "Have we helped the creator with their goal/issue?",
                    Order = 0,
                    Fields = new List<FormField>
                    {
                        new() { Label = "Accuracy",               Description = "Did the creator receive an accurate and complete solution for all the informed issues?",                                                          FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 0 },
                        new() { Label = "Tailoring",              Description = "Were the issues or expectations of the creator met with the right level of personalisation?",                                                    FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 1 },
                        new() { Label = "Obviation & Next Steps", Description = "Has the creator been equipped with relevant obviation opportunities and next steps?",                                                            FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 2 },
                    }
                },
                new()
                {
                    Title = "Creator Critical – Effort",
                    Description = "How much effort was it for the creator to get a resolution?",
                    Order = 1,
                    Fields = new List<FormField>
                    {
                        new() { Label = "Responsiveness",         Description = "Have we set and/or kept expectations with regards to timely and proactive follow-up communications?",                                           FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 0 },
                        new() { Label = "Internal Coordination",  Description = "Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?",                                FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 1 },
                        new() { Label = "Workflows Adherence",    Description = "Did we minimise creator effort by following correct workflows?",                                                                                FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 2 },
                        new() { Label = "Creator Feedback",       Description = "Was the creator reassured that their feedback was captured and addressed?",                                                                     FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true,  Order = 3 },
                        new() { Label = "CSAT Survey",            Description = "Was the creator appropriately asked to provide feedback through a CSAT survey?",                                                               FieldType = FieldType.Rating, MaxRating = 1, IsRequired = false, Order = 4 },
                    }
                },
                new()
                {
                    Title = "Creator Critical – Engagement",
                    Description = "How did we make the creator feel during their interaction?",
                    Order = 2,
                    Fields = new List<FormField>
                    {
                        new() { Label = "Clarity",  Description = "Has the creator received clear communication through the use of correct language and effective questioning?",                                                  FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 0 },
                        new() { Label = "Empathy",  Description = "Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?",                                           FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 1 },
                        new() { Label = "Tone",     Description = "Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?",                                FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 2 },
                    }
                },
                new()
                {
                    Title = "Business Critical",
                    Description = "Non-compensatory business-critical competencies — failure in any one triggers an auto-fail for this category.",
                    Order = 3,
                    Fields = new List<FormField>
                    {
                        new() { Label = "Due Diligence", Description = "Did the agent complete all required due-diligence steps before responding or escalating?",                                                               FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 0 },
                        new() { Label = "Issue Tagging", Description = "Was the case correctly tagged / categorised using Neo Categorization?",                                                                                  FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 1 },
                    }
                },
                new()
                {
                    Title = "Compliance Critical",
                    Description = "Non-compensatory compliance competencies — failure in any one triggers an auto-fail for this category.",
                    Order = 4,
                    Fields = new List<FormField>
                    {
                        new() { Label = "Authentication",     Description = "Did the agent follow the correct authentication process before discussing account or creator details?",                                              FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 0 },
                        new() { Label = "Keep YouTube Safe",  Description = "Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?",                                   FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 1 },
                        new() { Label = "Policy",             Description = "Did the agent correctly apply and communicate YouTube policies relevant to the creator's issue?",                                                   FieldType = FieldType.Rating, MaxRating = 1, IsRequired = true, Order = 2 },
                    }
                },
            }
        };
        EvaluationForms.Add(ytForm);
        await SaveChangesAsync();
        } // end if (!await Projects.AnyAsync(...))

        // ── YouTube IQA Knowledge Base (assessment guidelines for AI Audit) ────
        // This block runs every startup — guarded only by the source-level check —
        // so it backfills existing databases that were created before the KB was added.
        // ytProject is set above when the project was just created; for pre-existing
        // databases we locate the YouTube project via its unique LOB rather than by name.
        var ytProjectForKb = ytProject
            ?? await Lobs
                   .Where(l => l.Name == "CSO")
                   .Select(l => l.Project)
                   .FirstOrDefaultAsync();
        if (ytProjectForKb != null && !await KnowledgeSources.AnyAsync(s => s.ProjectId == ytProjectForKb.Id))
        {
            var ytKbSource = new KnowledgeSource
            {
                Name = "YouTube IQA Assessment Guidelines",
                ConnectorType = "ManualUpload",
                Description = "YouTube Creator Support Operations IQA framework — competency definitions, assessment guidelines, and scoring criteria used for quality evaluation.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastSyncedAt = DateTime.UtcNow,
                ProjectId = ytProjectForKb.Id,
                Documents = new List<KnowledgeDocument>
                {
                    new()
                    {
                        Title = "YouTube CSO IQA — Assessment Guidelines",
                        FileName = "YouTube_CSO_IQA_Assessment_Guidelines.pdf",
                        Tags = "YouTube,IQA,Guidelines,Assessment,Quality",
                        UploadedAt = DateTime.UtcNow,
                        Content = @"YouTube Creator Support Operations — IQA Assessment Guidelines

OVERVIEW
========
This document defines the assessment guidelines for all competencies evaluated in the YouTube CSO Internal Quality Assurance (IQA) framework. All competencies use a non-compensatory Pass / Fail scale. A PASS is awarded when the competency is fully met or not applicable (Yes / NA). A FAIL triggers an auto-fail for the relevant category.


CREATOR CRITICAL — EFFECTIVENESS
=================================
Category Purpose: Have we helped the creator with their goal/issue?

1. ACCURACY
   Assessment Question: Did the creator receive an accurate and complete solution for all the informed issues?
   PASS criteria: The agent provided a correct, complete answer that fully addressed every issue raised by the creator. No misinformation or omissions that would require the creator to contact YouTube again for the same matter.
   FAIL criteria: The agent gave inaccurate information, addressed only part of the issue, or the creator was not given a usable resolution.

2. TAILORING
   Assessment Question: Were the issues or expectations of the creator met with the right level of personalisation?
   PASS criteria: The agent demonstrated awareness of the creator's specific context (channel type, issue history, tone preference) and adapted the response accordingly. Generic, copy-paste responses are not sufficient when personalisation was possible.
   FAIL criteria: Response was templated or generic in a situation where personalised support was clearly required.

3. OBVIATION & NEXT STEPS
   Assessment Question: Has the creator been equipped with relevant obviation opportunities and next steps?
   PASS criteria: The agent proactively offered relevant self-service resources, explained preventive steps, or outlined clear next actions so the creator can avoid recurrence or knows what to do next.
   FAIL criteria: Interaction closed without equipping the creator with next steps or relevant resources where these existed.


CREATOR CRITICAL — EFFORT
==========================
Category Purpose: How much effort was it for the creator to get a resolution?

4. RESPONSIVENESS
   Assessment Question: Have we set and/or kept expectations with regards to timely and proactive follow-up communications?
   PASS criteria: The agent acknowledged the creator's issue promptly, set clear timelines for resolution, and followed up proactively when delays occurred. No unexplained silences or missed deadlines.
   FAIL criteria: Creator had to chase for updates, or expectations around timelines were not set or not met without communication.

5. INTERNAL COORDINATION
   Assessment Question: Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?
   PASS criteria: Where escalation or consultation was required, the agent facilitated this seamlessly — creator was not asked to repeat information or contact other teams themselves.
   FAIL criteria: Creator was bounced between teams, asked to re-explain their issue, or the agent failed to engage the right internal resource.

6. WORKFLOWS ADHERENCE
   Assessment Question: Did we minimise creator effort by following correct workflows?
   PASS criteria: The agent followed all prescribed workflows (Neo case management, escalation paths, template use) correctly so the creator's case was handled efficiently without unnecessary loops.
   FAIL criteria: Deviations from required workflows led to delays, rework, or additional creator effort.

7. CREATOR FEEDBACK
   Assessment Question: Was the creator reassured that their feedback was captured and addressed?
   PASS criteria: Where the creator raised product feedback, platform issues, or suggestions, the agent acknowledged these, confirmed they would be logged, and set appropriate expectations.
   FAIL criteria: Creator feedback was dismissed, ignored, or no acknowledgement was given that it would be captured.

8. CSAT SURVEY
   Assessment Question: Was the creator appropriately asked to provide feedback through a CSAT survey?
   PASS criteria: The agent invited the creator to complete a CSAT survey in accordance with current guidelines (correct timing, correct channel, no coaching or influencing language).
   FAIL criteria: Survey invitation was omitted, delivered at the wrong time, or the agent used language that could influence the creator's rating.


CREATOR CRITICAL — ENGAGEMENT
===============================
Category Purpose: How did we make the creator feel during their interaction?

9. CLARITY
   Assessment Question: Has the creator received clear communication through the use of correct language and effective questioning?
   PASS criteria: All written or verbal communication was clear, concise, and free of jargon. The agent used effective questioning to confirm understanding and avoid ambiguity.
   FAIL criteria: Communication was unclear, used unexplained technical terms, or the agent failed to confirm understanding, leading to confusion.

10. EMPATHY
    Assessment Question: Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?
    PASS criteria: The agent acknowledged the creator's situation, demonstrated genuine understanding of the urgency or emotional weight of the issue, and responded in a way that made the creator feel heard and valued.
    FAIL criteria: The agent was dismissive, failed to acknowledge frustration or urgency, or responded in a way that felt robotic or uncaring.

11. TONE
    Assessment Question: Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?
    PASS criteria: All communications were professional, warm, and consistent with YouTube's Tone & Voice guidelines throughout — including greetings, closings, and any difficult moments in the conversation.
    FAIL criteria: Tone was inappropriate, inconsistent, or did not align with YouTube's brand guidelines (e.g., overly formal, casual, or passive-aggressive).


BUSINESS CRITICAL
==================
Category Purpose: Non-compensatory business-critical competencies — failure in any one triggers an auto-fail for this category.

12. DUE DILIGENCE
    Assessment Question: Did the agent complete all required due-diligence steps before responding or escalating?
    PASS criteria: The agent completed all mandatory checks (account verification, case history review, policy lookup) before providing a response or escalating the case.
    FAIL criteria: The agent responded or escalated without completing required due-diligence checks, risking incorrect advice or security breaches.

13. ISSUE TAGGING
    Assessment Question: Was the case correctly tagged / categorised using Neo Categorization?
    PASS criteria: The case was tagged with the correct primary and secondary categories in the Neo system, enabling accurate reporting and routing.
    FAIL criteria: Case was tagged incorrectly, incompletely, or not tagged at all.


COMPLIANCE CRITICAL
====================
Category Purpose: Non-compensatory compliance competencies — failure in any one triggers an auto-fail for this category.

14. AUTHENTICATION
    Assessment Question: Did the agent follow the correct authentication process before discussing account or creator details?
    PASS criteria: The agent followed the prescribed authentication workflow in full before accessing or disclosing any account-specific information. For cases where authentication was not required, this competency is marked N/A (PASS).
    FAIL criteria: The agent disclosed account information or took account actions without completing the authentication process.

15. KEEP YOUTUBE SAFE
    Assessment Question: Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?
    PASS criteria: The agent complied fully with all YouTube Trust & Safety and Content Policy obligations — including correct escalation of policy violations, not engaging with harmful content, and protecting creator and user privacy.
    FAIL criteria: The agent failed to escalate a T&S concern, shared information that could endanger a creator, or otherwise breached safety obligations.

16. POLICY
    Assessment Question: Did the agent correctly apply and communicate YouTube policies relevant to the creator's issue?
    PASS criteria: The agent applied the correct YouTube policy to the creator's situation, communicated the policy clearly and accurately, and did not misrepresent or omit relevant policy details.
    FAIL criteria: The agent applied the wrong policy, communicated a policy inaccurately, or failed to inform the creator of a policy directly relevant to their issue.


SCORING SUMMARY
===============
Total competencies: 16
Pass/Fail per competency (non-compensatory within each category).

Auto-fail categories:
  • Business Critical: Failure in Due Diligence OR Issue Tagging = auto-fail for this category.
  • Compliance Critical: Failure in Authentication OR Keep YouTube Safe OR Policy = auto-fail for this category.

Overall score = (number of PASS competencies / total assessed competencies) × 100%.
A score of 100% is expected for Business Critical and Compliance Critical competencies.
",
                        ContentSizeBytes = 7200,
                    }
                }
            };
            KnowledgeSources.Add(ytKbSource);
            await SaveChangesAsync();
        }
    }
}
