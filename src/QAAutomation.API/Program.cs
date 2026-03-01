using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.Models;
using QAAutomation.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "QA Automation API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=qa_automation.db"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Core services — always registered
builder.Services.AddScoped<IAiConfigService, DbAiConfigService>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

// AI services: runtime selection based on DB config (AiConfig.LlmEndpoint non-empty → real LLM)
// Both real and mock are registered; a factory wrapper picks at request time.
builder.Services.AddScoped<AzureOpenAIAutoAuditService>();
builder.Services.AddScoped<MockAutoAuditService>();
builder.Services.AddScoped<AzureOpenAISentimentService>();
builder.Services.AddScoped<MockSentimentService>();

builder.Services.AddScoped<IAutoAuditService, RuntimeAutoAuditService>();
builder.Services.AddScoped<ISentimentService, RuntimeSentimentService>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    foreach (var sql in new[] {
        "ALTER TABLE EvaluationResults ADD COLUMN AgentName TEXT NULL",
        "ALTER TABLE EvaluationResults ADD COLUMN CallReference TEXT NULL",
        "ALTER TABLE EvaluationResults ADD COLUMN CallDate TEXT NULL",
        "ALTER TABLE Parameters ADD COLUMN EvaluationType TEXT NOT NULL DEFAULT 'LLM'",
        "ALTER TABLE Parameters ADD COLUMN ProjectId INTEGER NULL",
        "ALTER TABLE ParameterClubs ADD COLUMN ProjectId INTEGER NULL",
        "ALTER TABLE RatingCriteria ADD COLUMN ProjectId INTEGER NULL",
        "ALTER TABLE KnowledgeSources ADD COLUMN ProjectId INTEGER NULL",
        "ALTER TABLE EvaluationForms ADD COLUMN LobId INTEGER NULL",
    })
    {
        try { db.Database.ExecuteSqlRaw(sql); }
        catch (Exception ex) when (ex.Message.Contains("duplicate column"))
        {
            // Column already exists — expected on fresh DBs created by EnsureCreated
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema migration failed for: {Sql}", sql);
        }
    }
    await db.SeedAsync();

    // For existing databases: ensure all data is associated to default project/LOB
    await MigrateExistingDataToDefaultProjectAsync(db, logger);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ── Migration helper: bind existing un-scoped data to the default project ────
static async Task MigrateExistingDataToDefaultProjectAsync(AppDbContext db, ILogger logger)
{
    try
    {
        // Find or create default project
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Name == "Capital One");
        if (project == null) return; // seed hasn't run yet or already migrated

        var lob = await db.Lobs.FirstOrDefaultAsync(l => l.ProjectId == project.Id && l.Name == "Customer Support Call");
        if (lob == null) return;

        bool changed = false;

        // Bind orphaned parameters
        var orphanParams = await db.Parameters.Where(p => p.ProjectId == null).ToListAsync();
        foreach (var p in orphanParams) { p.ProjectId = project.Id; changed = true; }

        // Bind orphaned parameter clubs
        var orphanClubs = await db.ParameterClubs.Where(c => c.ProjectId == null).ToListAsync();
        foreach (var c in orphanClubs) { c.ProjectId = project.Id; changed = true; }

        // Bind orphaned rating criteria
        var orphanCriteria = await db.RatingCriteria.Where(rc => rc.ProjectId == null).ToListAsync();
        foreach (var rc in orphanCriteria) { rc.ProjectId = project.Id; changed = true; }

        // Bind orphaned evaluation forms to the default LOB
        var orphanForms = await db.EvaluationForms.Where(f => f.LobId == null).ToListAsync();
        foreach (var f in orphanForms) { f.LobId = lob.Id; changed = true; }

        // Bind orphaned knowledge sources
        var orphanKb = await db.KnowledgeSources.Where(s => s.ProjectId == null).ToListAsync();
        foreach (var s in orphanKb) { s.ProjectId = project.Id; changed = true; }

        // Ensure admin user has access to this project
        var admin = await db.AppUsers.FirstOrDefaultAsync(u => u.Username == "admin");
        if (admin != null && !await db.UserProjectAccesses.AnyAsync(a => a.UserId == admin.Id && a.ProjectId == project.Id))
        {
            db.UserProjectAccesses.Add(new UserProjectAccess { UserId = admin.Id, ProjectId = project.Id, GrantedAt = DateTime.UtcNow });
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Existing data migration to default project failed (non-fatal)");
    }
}

// Make Program accessible to test project
public partial class Program { }
