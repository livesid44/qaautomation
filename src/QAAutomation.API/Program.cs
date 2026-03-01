using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
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
        "ALTER TABLE Parameters ADD COLUMN EvaluationType TEXT NOT NULL DEFAULT 'LLM'"
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
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program accessible to test project
public partial class Program { }
