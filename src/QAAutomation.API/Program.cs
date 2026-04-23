using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.Models;
using QAAutomation.API.Services;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Database provider detection ───────────────────────────────────────────────
// If DefaultConnection is empty or looks like a SQLite path, use SQLite.
// Any connection string containing SQL Server keywords (Server=, Initial Catalog=,
// database=, Trusted_Connection=) is treated as SQL Server.
static bool IsSqlServerConnectionString(string? connStr) =>
    !string.IsNullOrWhiteSpace(connStr) &&
    (connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
     connStr.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
     connStr.Contains("database=", StringComparison.OrdinalIgnoreCase) ||
     connStr.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
     connStr.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase));

static string ResolveSqliteConnectionString(string? connStr, string contentRootPath) =>
    string.IsNullOrEmpty(connStr)
        ? $"Data Source={Path.Combine(contentRootPath, "qa_automation.db")}"
        : connStr;

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
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (IsSqlServerConnectionString(connStr))
        options.UseSqlServer(connStr);
    else
        options.UseSqlite(ResolveSqliteConnectionString(connStr, builder.Environment.ContentRootPath));
});

// DbContextFactory needed by AuditLogService (writes audit entries in independent scopes)
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (IsSqlServerConnectionString(connStr))
        options.UseSqlServer(connStr);
    else
        options.UseSqlite(ResolveSqliteConnectionString(connStr, builder.Environment.ContentRootPath));
}, ServiceLifetime.Scoped);

// Audit logging — captures PII/SPII events and external API calls per tenant
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Core services — always registered
builder.Services.AddScoped<IAiConfigService, DbAiConfigService>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

// Named HttpClient for fetching web URLs into the Knowledge Bank
builder.Services.AddHttpClient("kb-url-fetch")
    .ConfigureHttpClient(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(20);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("QAAutomation-KnowledgeBot/1.0");
    });

// Pipeline service — fetches transcripts from URLs / SFTP / SharePoint / recording platforms
builder.Services.AddHttpClient("pipeline")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddSingleton<PipelineProgressHub>();
builder.Services.AddScoped<ICallPipelineService, CallPipelineService>();

// Azure Speech-to-Text — transcribes audio recordings before QA scoring
builder.Services.AddHttpClient("speech")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(12)); // batch jobs can take a while
builder.Services.AddScoped<AzureSpeechService>();
builder.Services.AddScoped<MockAzureSpeechService>();

// Google Gemini (LLM + sentiment) and Google Cloud Speech-to-Text
builder.Services.AddHttpClient("gemini")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddScoped<GoogleGeminiAutoAuditService>();
builder.Services.AddScoped<GoogleGeminiSentimentService>();
builder.Services.AddScoped<GoogleSpeechService>();

// IAzureSpeechService: runtime-selected (Azure or Google) based on AiConfig.SpeechProvider
builder.Services.AddScoped<IAzureSpeechService, RuntimeSpeechService>();

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

// TNI (Training Needs Identification) — LLM content + MCQ generation
builder.Services.AddScoped<TniGenerationService>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Detect the actual provider used by the registered DbContext (not from config),
    // so that test factories that swap in SQLite in-memory are handled correctly.
    var isSqlServer = db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    if (isSqlServer)
    {
        // SQL Server: EnsureCreated creates all tables that don't yet exist without
        // touching ones that do, so it is safe to call on an existing database.
        db.Database.EnsureCreated();
    }
    else
    {
        // SQLite: generate the full schema DDL and apply each statement with IF NOT EXISTS
        // so existing tables/indexes are preserved and any missing ones are created.
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
    }

    // Column-level migrations for tables that existed before new columns were added.
    // We check existence first so EF Core never logs a "failed DbCommand" for expected skips.
    // table/column names are compile-time constants — validated to be alphanumeric+underscore
    // to prevent any possibility of SQL injection even if the list were ever changed.
    static bool ColumnExists(AppDbContext ctx, string table, string column, bool sqlServer)
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
            if (sqlServer)
            {
                // INFORMATION_SCHEMA is available on all SQL Server / Azure SQL versions.
                // If the TABLE itself does not exist (e.g. freshly created by EnsureCreated()),
                // return true so we skip the ALTER TABLE — EnsureCreated() already created the
                // table with all current model columns and no further migration is needed.
                // Use parameterized queries to guard against any future dynamic input.
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @t";
                var ptbl = cmd.CreateParameter(); ptbl.ParameterName = "@t"; ptbl.Value = table; cmd.Parameters.Add(ptbl);
                var tableCount = (int)(cmd.ExecuteScalar() ?? 0);
                if (tableCount == 0) return true; // table absent → EnsureCreated owns it; skip ALTER

                cmd.Parameters.Clear();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c";
                var pt = cmd.CreateParameter(); pt.ParameterName = "@t"; pt.Value = table; cmd.Parameters.Add(pt);
                var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = column; cmd.Parameters.Add(pc);
                return (int)(cmd.ExecuteScalar() ?? 0) > 0;
            }
            else
            {
                // SQLite PRAGMA doesn't support parameterized identifiers; names are allowlisted above.
                cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
                return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
            }
        }
        finally { if (openedByUs) conn.Close(); }
    }

    // Column migration definitions: (table, column, sqliteSql, sqlServerSql)
    // SQL Server uses NVARCHAR/INT/FLOAT/BIT instead of SQLite TEXT/INTEGER/REAL.
    // SQL Server ALTER TABLE does not support the COLUMN keyword.
    var columnMigrations = new (string Table, string Column, string SqliteSql, string SqlServerSql)[]
    {
        ("EvaluationResults", "AgentName",           "ALTER TABLE EvaluationResults ADD COLUMN AgentName TEXT NULL",                        "ALTER TABLE EvaluationResults ADD AgentName NVARCHAR(200) NULL"),
        ("EvaluationResults", "CallReference",        "ALTER TABLE EvaluationResults ADD COLUMN CallReference TEXT NULL",                   "ALTER TABLE EvaluationResults ADD CallReference NVARCHAR(100) NULL"),
        ("EvaluationResults", "CallDate",             "ALTER TABLE EvaluationResults ADD COLUMN CallDate TEXT NULL",                        "ALTER TABLE EvaluationResults ADD CallDate DATETIME2 NULL"),
        ("Parameters",        "EvaluationType",       "ALTER TABLE Parameters ADD COLUMN EvaluationType TEXT NOT NULL DEFAULT 'LLM'",        "ALTER TABLE Parameters ADD EvaluationType NVARCHAR(50) NOT NULL DEFAULT 'LLM'"),
        ("Parameters",        "ProjectId",            "ALTER TABLE Parameters ADD COLUMN ProjectId INTEGER NULL",                           "ALTER TABLE Parameters ADD ProjectId INT NULL"),
        ("ParameterClubs",    "ProjectId",            "ALTER TABLE ParameterClubs ADD COLUMN ProjectId INTEGER NULL",                       "ALTER TABLE ParameterClubs ADD ProjectId INT NULL"),
        ("RatingCriteria",    "ProjectId",            "ALTER TABLE RatingCriteria ADD COLUMN ProjectId INTEGER NULL",                       "ALTER TABLE RatingCriteria ADD ProjectId INT NULL"),
        ("KnowledgeSources",  "ProjectId",            "ALTER TABLE KnowledgeSources ADD COLUMN ProjectId INTEGER NULL",                     "ALTER TABLE KnowledgeSources ADD ProjectId INT NULL"),
        ("EvaluationForms",   "LobId",                "ALTER TABLE EvaluationForms ADD COLUMN LobId INTEGER NULL",                         "ALTER TABLE EvaluationForms ADD LobId INT NULL"),
        ("AiConfigs",         "LlmDeployment",        "ALTER TABLE AiConfigs ADD COLUMN LlmDeployment TEXT NULL DEFAULT 'gpt-4o'",          "ALTER TABLE AiConfigs ADD LlmDeployment NVARCHAR(200) NULL DEFAULT 'gpt-4o'"),
        ("AiConfigs",         "LlmTemperature",       "ALTER TABLE AiConfigs ADD COLUMN LlmTemperature REAL NULL DEFAULT 0.1",              "ALTER TABLE AiConfigs ADD LlmTemperature FLOAT NULL DEFAULT 0.1"),
        ("AiConfigs",         "SentimentProvider",    "ALTER TABLE AiConfigs ADD COLUMN SentimentProvider TEXT NULL DEFAULT 'AzureOpenAI'", "ALTER TABLE AiConfigs ADD SentimentProvider NVARCHAR(50) NULL DEFAULT 'AzureOpenAI'"),
        ("AiConfigs",         "LanguageEndpoint",     "ALTER TABLE AiConfigs ADD COLUMN LanguageEndpoint TEXT NOT NULL DEFAULT ''",         "ALTER TABLE AiConfigs ADD LanguageEndpoint NVARCHAR(500) NOT NULL DEFAULT ''"),
        // API key columns use empty-string default (not NULL) because the C# model uses non-nullable
        // string and the service guards against blank values before saving.
        ("AiConfigs",         "LanguageApiKey",       "ALTER TABLE AiConfigs ADD COLUMN LanguageApiKey TEXT NOT NULL DEFAULT ''",           "ALTER TABLE AiConfigs ADD LanguageApiKey NVARCHAR(500) NOT NULL DEFAULT ''"),
        ("AiConfigs",         "RagTopK",              "ALTER TABLE AiConfigs ADD COLUMN RagTopK INTEGER NULL DEFAULT 3",                    "ALTER TABLE AiConfigs ADD RagTopK INT NULL DEFAULT 3"),
        ("FormFields",        "Description",          "ALTER TABLE FormFields ADD COLUMN Description TEXT NULL",                            "ALTER TABLE FormFields ADD Description NVARCHAR(MAX) NULL"),
        ("EvaluationResults", "OverallReasoning",     "ALTER TABLE EvaluationResults ADD COLUMN OverallReasoning TEXT NULL",                "ALTER TABLE EvaluationResults ADD OverallReasoning NVARCHAR(MAX) NULL"),
        ("EvaluationResults", "SentimentJson",        "ALTER TABLE EvaluationResults ADD COLUMN SentimentJson TEXT NULL",                   "ALTER TABLE EvaluationResults ADD SentimentJson NVARCHAR(MAX) NULL"),
        ("EvaluationResults", "FieldReasoningJson",   "ALTER TABLE EvaluationResults ADD COLUMN FieldReasoningJson TEXT NULL",              "ALTER TABLE EvaluationResults ADD FieldReasoningJson NVARCHAR(MAX) NULL"),
        // Project-level PII/SPII protection settings
        ("Projects",          "PiiProtectionEnabled", "ALTER TABLE Projects ADD COLUMN PiiProtectionEnabled INTEGER NOT NULL DEFAULT 0",    "ALTER TABLE Projects ADD PiiProtectionEnabled BIT NOT NULL DEFAULT 0"),
        ("Projects",          "PiiRedactionMode",     "ALTER TABLE Projects ADD COLUMN PiiRedactionMode TEXT NOT NULL DEFAULT 'Redact'",    "ALTER TABLE Projects ADD PiiRedactionMode NVARCHAR(20) NOT NULL DEFAULT 'Redact'"),
        // Google provider fields (added alongside Google Gemini / Google STT support)
        ("AiConfigs",         "GoogleApiKey",         "ALTER TABLE AiConfigs ADD COLUMN GoogleApiKey TEXT NOT NULL DEFAULT ''",             "ALTER TABLE AiConfigs ADD GoogleApiKey NVARCHAR(500) NOT NULL DEFAULT ''"),
        ("AiConfigs",         "GoogleGeminiModel",    "ALTER TABLE AiConfigs ADD COLUMN GoogleGeminiModel TEXT NOT NULL DEFAULT 'gemini-1.5-pro'", "ALTER TABLE AiConfigs ADD GoogleGeminiModel NVARCHAR(100) NOT NULL DEFAULT 'gemini-1.5-pro'"),
        ("AiConfigs",         "SpeechProvider",       "ALTER TABLE AiConfigs ADD COLUMN SpeechProvider TEXT NOT NULL DEFAULT 'Azure'",      "ALTER TABLE AiConfigs ADD SpeechProvider NVARCHAR(50) NOT NULL DEFAULT 'Azure'"),
        ("EvaluationForms",   "ScoringMethod",         "ALTER TABLE EvaluationForms ADD COLUMN ScoringMethod INTEGER NOT NULL DEFAULT 0",     "ALTER TABLE EvaluationForms ADD ScoringMethod INT NOT NULL DEFAULT 0"),
        ("TrainingPlans",     "IsAutoGenerated",       "ALTER TABLE TrainingPlans ADD COLUMN IsAutoGenerated INTEGER NOT NULL DEFAULT 0",      "ALTER TABLE TrainingPlans ADD IsAutoGenerated BIT NOT NULL DEFAULT 0"),
        ("TrainingPlans",     "LlmTrainingContent",    "ALTER TABLE TrainingPlans ADD COLUMN LlmTrainingContent TEXT NULL",                     "ALTER TABLE TrainingPlans ADD LlmTrainingContent NVARCHAR(MAX) NULL"),
        ("TrainingPlans",     "AssessmentJson",        "ALTER TABLE TrainingPlans ADD COLUMN AssessmentJson TEXT NULL",                          "ALTER TABLE TrainingPlans ADD AssessmentJson NVARCHAR(MAX) NULL"),
        ("TrainingPlans",     "ContentGeneratedAt",    "ALTER TABLE TrainingPlans ADD COLUMN ContentGeneratedAt TEXT NULL",                      "ALTER TABLE TrainingPlans ADD ContentGeneratedAt DATETIME2 NULL"),
        ("TrainingPlans",     "AssessmentPassMark",    "ALTER TABLE TrainingPlans ADD COLUMN AssessmentPassMark INTEGER NOT NULL DEFAULT 70",    "ALTER TABLE TrainingPlans ADD AssessmentPassMark INT NOT NULL DEFAULT 70"),
    };

    foreach (var (table, column, sqliteSql, sqlServerSql) in columnMigrations)
    {
        if (ColumnExists(db, table, column, isSqlServer)) continue; // already up-to-date, skip cleanly
        var migrationSql = isSqlServer ? sqlServerSql : sqliteSql;
        try { db.Database.ExecuteSqlRaw(migrationSql); }
        catch (Exception ex) { logger.LogWarning(ex, "Column migration failed: {Sql}", migrationSql); }
    }

    // Table-level migrations for SQL Server — EnsureCreated() only creates tables on a completely
    // empty database; it does NOT add tables that are missing from an existing database.
    // Each entry: (guard SQL that returns 0 when table is absent, DDL to create it)
    if (isSqlServer)
    {
        var tableMigrations = new (string CheckSql, string CreateSql)[]
        {
            (
                "SELECT COUNT(*) FROM sys.tables WHERE name = 'HumanFieldScores'",
                @"CREATE TABLE [HumanFieldScores] (
                    [Id]                  int            NOT NULL IDENTITY CONSTRAINT [PK_HumanFieldScores] PRIMARY KEY,
                    [HumanReviewItemId]   int            NOT NULL,
                    [FieldId]             int            NOT NULL,
                    [AiScore]             float          NOT NULL,
                    [HumanScore]          float          NOT NULL,
                    [Comment]             nvarchar(1000) NULL,
                    CONSTRAINT [FK_HumanFieldScores_HumanReviewItems] FOREIGN KEY ([HumanReviewItemId]) REFERENCES [HumanReviewItems] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_HumanFieldScores_FormFields]       FOREIGN KEY ([FieldId])             REFERENCES [FormFields]      ([Id]) ON DELETE NO ACTION
                );
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HumanFieldScores_HumanReviewItemId' AND object_id = OBJECT_ID('HumanFieldScores'))
                    CREATE INDEX [IX_HumanFieldScores_HumanReviewItemId] ON [HumanFieldScores] ([HumanReviewItemId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HumanFieldScores_FieldId' AND object_id = OBJECT_ID('HumanFieldScores'))
                    CREATE INDEX [IX_HumanFieldScores_FieldId] ON [HumanFieldScores] ([FieldId]);"
            ),
        };

        var conn2 = db.Database.GetDbConnection();
        bool opened2 = conn2.State == ConnectionState.Closed;
        if (opened2) conn2.Open();
        try
        {
            foreach (var (checkSql, createSql) in tableMigrations)
            {
                using var cmd = conn2.CreateCommand();
                cmd.CommandText = checkSql;
                var exists2 = (int)(cmd.ExecuteScalar() ?? 0);
                if (exists2 > 0) continue; // table already present — skip
                try { db.Database.ExecuteSqlRaw(createSql); }
                catch (Exception ex) { logger.LogWarning(ex, "Table migration failed for check: {Check}", checkSql); }
            }

            // If the table already existed with ON DELETE CASCADE on FieldId (from a previous
            // migration), alter it to NO ACTION to avoid SQL Server cascade path conflicts.
            try
            {
                using var chk = conn2.CreateCommand();
                chk.CommandText = @"
                    SELECT COUNT(*)
                    FROM   sys.foreign_keys
                    WHERE  name = 'FK_HumanFieldScores_FormFields'
                      AND  delete_referential_action_desc = 'CASCADE'";
                var hasCascade = (int)(chk.ExecuteScalar() ?? 0);
                if (hasCascade > 0)
                {
                    db.Database.ExecuteSqlRaw("ALTER TABLE [HumanFieldScores] DROP CONSTRAINT [FK_HumanFieldScores_FormFields]");
                    db.Database.ExecuteSqlRaw(@"ALTER TABLE [HumanFieldScores]
                        ADD CONSTRAINT [FK_HumanFieldScores_FormFields]
                        FOREIGN KEY ([FieldId]) REFERENCES [FormFields]([Id]) ON DELETE NO ACTION");
                }
            }
            catch (Exception ex) { logger.LogDebug(ex, "FK migration for HumanFieldScores skipped"); }
        }
        finally { if (opened2) conn2.Close(); }
    }

    await db.SeedAsync();
    await db.SeedYouTubeAsync();

    // For existing databases: ensure all data is associated to default project/LOB
    await MigrateExistingDataToDefaultProjectAsync(db, logger);

    // ── Restart recovery: reset stale pipeline job states ──────────────────
    // Any job left in "Running" state was interrupted by a previous application
    // shutdown (the background Task.Run was killed).  Reset these jobs back to
    // "Pending" so they can be re-triggered by the user.  Items that were
    // mid-flight ("Processing") are also reset to "Pending" so they are retried.
    var staleJobs = await db.CallPipelineJobs
        .Include(j => j.Items)
        .Where(j => j.Status == "Running")
        .ToListAsync();

    if (staleJobs.Count > 0)
    {
        foreach (var staleJob in staleJobs)
        {
            staleJob.Status = "Pending";
            foreach (var item in staleJob.Items.Where(i => i.Status == "Processing"))
                item.Status = "Pending";
        }
        await db.SaveChangesAsync();
        logger.LogWarning(
            "Startup recovery: reset {Count} stale Running pipeline job(s) to Pending so they can be re-triggered.",
            staleJobs.Count);
    }
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
