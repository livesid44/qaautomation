using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.Models;
using QAAutomation.API.Services;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // The Web layer sends empty strings (or null after ASP.NET Core converts empty form
    // fields to null) for optional fields like LlmApiKey / LanguageApiKey.  Suppress the
    // implicit [Required] that <Nullable>enable</Nullable> adds to non-nullable string
    // properties so those empty/null values are accepted without triggering 400 responses.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
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
{
    // Build an absolute path so the DB file is always in the project/content-root
    // directory regardless of the working directory (VS, dotnet run, IIS Express, etc.).
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connStr))
        connStr = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "qa_automation.db")}";
    options.UseSqlite(connStr);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Core services — always registered
builder.Services.AddScoped<IAiConfigService, DbAiConfigService>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

// Pipeline service — fetches transcripts from URLs / SFTP / SharePoint / recording platforms
builder.Services.AddHttpClient("pipeline")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddScoped<ICallPipelineService, CallPipelineService>();

// Azure Speech-to-Text — transcribes audio recordings before QA scoring
builder.Services.AddHttpClient("speech")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(12)); // batch jobs can take a while
builder.Services.AddScoped<AzureSpeechService>();
builder.Services.AddScoped<MockAzureSpeechService>();
builder.Services.AddScoped<IAzureSpeechService, RuntimeSpeechTranscriptionService>();

// AI services: runtime selection based on DB config (AiConfig.LlmEndpoint non-empty → real LLM)
// Both real and mock are registered; a factory wrapper picks at request time.
builder.Services.AddScoped<AzureOpenAIAutoAuditService>();
builder.Services.AddScoped<MockAutoAuditService>();
builder.Services.AddScoped<AzureOpenAISentimentService>();
builder.Services.AddScoped<MockSentimentService>();

builder.Services.AddScoped<IAutoAuditService, RuntimeAutoAuditService>();
builder.Services.AddScoped<ISentimentService, RuntimeSentimentService>();

// Insights Chat — NL→SQL analytics chatbot using tenant LLM
builder.Services.AddScoped<InsightsChatService>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Robust schema creation: EnsureCreated() bails out when any tables already exist,
    // leaving partial/legacy databases with missing tables. Instead we generate the full
    // schema DDL and apply each statement with IF NOT EXISTS so existing tables/indexes
    // are preserved and any missing ones are created.
    var createScript = db.Database.GenerateCreateScript();
    foreach (var rawStmt in createScript.Split(";\n", StringSplitOptions.RemoveEmptyEntries))
    {
        var stmt = rawStmt.Trim();
        if (string.IsNullOrWhiteSpace(stmt)) continue;
        if (stmt.StartsWith("CREATE TABLE ", StringComparison.OrdinalIgnoreCase))
            stmt = "CREATE TABLE IF NOT EXISTS " + stmt["CREATE TABLE ".Length..];
        else if (stmt.StartsWith("CREATE UNIQUE INDEX ", StringComparison.OrdinalIgnoreCase))
            stmt = "CREATE UNIQUE INDEX IF NOT EXISTS " + stmt["CREATE UNIQUE INDEX ".Length..];
        else if (stmt.StartsWith("CREATE INDEX ", StringComparison.OrdinalIgnoreCase))
            stmt = "CREATE INDEX IF NOT EXISTS " + stmt["CREATE INDEX ".Length..];
        const int MaxLoggedStatementLength = 80;
        try { db.Database.ExecuteSqlRaw(stmt); }
        catch (Exception ex) { logger.LogDebug(ex, "Schema init skipped for: {Stmt}", stmt[..Math.Min(MaxLoggedStatementLength, stmt.Length)]); }
    }

    // Column-level migrations for tables that existed before new columns were added.
    // We check existence first so EF Core never logs a "failed DbCommand" for expected skips.
    // table/column names are compile-time constants — validated to be alphanumeric+underscore
    // to prevent any possibility of SQL injection even if the list were ever changed.
    static bool ColumnExists(AppDbContext ctx, string table, string column)
    {
        // Allowlist: only alphanumeric and underscore (all our table/column names qualify)
        if (!System.Text.RegularExpressions.Regex.IsMatch(table, @"^\w+$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(column, @"^\w+$"))
            return false; // refuse to run if name contains unexpected characters

        var conn = ctx.Database.GetDbConnection();
        bool openedByUs = conn.State == ConnectionState.Closed;
        if (openedByUs) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            // SQLite PRAGMA doesn't support parameterized identifiers; names are allowlisted above.
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }
        finally { if (openedByUs) conn.Close(); }
    }

    foreach (var (table, column, sql) in new (string, string, string)[] {
        ("EvaluationResults", "AgentName",          "ALTER TABLE EvaluationResults ADD COLUMN AgentName TEXT NULL"),
        ("EvaluationResults", "CallReference",       "ALTER TABLE EvaluationResults ADD COLUMN CallReference TEXT NULL"),
        ("EvaluationResults", "CallDate",            "ALTER TABLE EvaluationResults ADD COLUMN CallDate TEXT NULL"),
        ("Parameters",        "EvaluationType",      "ALTER TABLE Parameters ADD COLUMN EvaluationType TEXT NOT NULL DEFAULT 'LLM'"),
        ("Parameters",        "ProjectId",           "ALTER TABLE Parameters ADD COLUMN ProjectId INTEGER NULL"),
        ("ParameterClubs",    "ProjectId",           "ALTER TABLE ParameterClubs ADD COLUMN ProjectId INTEGER NULL"),
        ("RatingCriteria",    "ProjectId",           "ALTER TABLE RatingCriteria ADD COLUMN ProjectId INTEGER NULL"),
        ("KnowledgeSources",  "ProjectId",           "ALTER TABLE KnowledgeSources ADD COLUMN ProjectId INTEGER NULL"),
        ("EvaluationForms",   "LobId",               "ALTER TABLE EvaluationForms ADD COLUMN LobId INTEGER NULL"),
        // AiConfigs columns added after initial release
        ("AiConfigs",         "LlmDeployment",       "ALTER TABLE AiConfigs ADD COLUMN LlmDeployment TEXT NULL DEFAULT 'gpt-4o'"),
        ("AiConfigs",         "LlmTemperature",      "ALTER TABLE AiConfigs ADD COLUMN LlmTemperature REAL NULL DEFAULT 0.1"),
        ("AiConfigs",         "SentimentProvider",   "ALTER TABLE AiConfigs ADD COLUMN SentimentProvider TEXT NULL DEFAULT 'AzureOpenAI'"),
        ("AiConfigs",         "LanguageEndpoint",    "ALTER TABLE AiConfigs ADD COLUMN LanguageEndpoint TEXT NOT NULL DEFAULT ''"),
        // API key columns use empty-string default (not NULL) because the C# model uses non-nullable
        // string and the service guards against blank values before saving.
        ("AiConfigs",         "LanguageApiKey",      "ALTER TABLE AiConfigs ADD COLUMN LanguageApiKey TEXT NOT NULL DEFAULT ''"),
        ("AiConfigs",         "RagTopK",             "ALTER TABLE AiConfigs ADD COLUMN RagTopK INTEGER NULL DEFAULT 3"),
        ("FormFields",        "Description",         "ALTER TABLE FormFields ADD COLUMN Description TEXT NULL"),
        ("EvaluationResults", "OverallReasoning",    "ALTER TABLE EvaluationResults ADD COLUMN OverallReasoning TEXT NULL"),
        ("EvaluationResults", "SentimentJson",       "ALTER TABLE EvaluationResults ADD COLUMN SentimentJson TEXT NULL"),
        ("EvaluationResults", "FieldReasoningJson",  "ALTER TABLE EvaluationResults ADD COLUMN FieldReasoningJson TEXT NULL"),
    })
    {
        if (ColumnExists(db, table, column)) continue; // already up-to-date, skip cleanly
        try { db.Database.ExecuteSqlRaw(sql); }
        catch (Exception ex) { logger.LogWarning(ex, "Column migration failed: {Sql}", sql); }
    }
    await db.SeedAsync();
    await db.SeedYouTubeAsync();

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
        // Find the default project by creation order (first inserted) rather than by its
        // display name, so this works even when the project is renamed or the seed content
        // is customised for a different client.
        var project = await db.Projects.OrderBy(p => p.Id).FirstOrDefaultAsync();
        if (project == null) return; // seed hasn't run yet

        // Similarly, locate the first LOB for this project by creation order.
        var lob = await db.Lobs.OrderBy(l => l.Id).FirstOrDefaultAsync(l => l.ProjectId == project.Id);
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
