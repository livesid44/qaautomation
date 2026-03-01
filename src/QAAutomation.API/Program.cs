using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;

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

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // Add new columns if they don't exist (for existing databases — SQLite throws on duplicate column)
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    foreach (var sql in new[] {
        "ALTER TABLE EvaluationResults ADD COLUMN AgentName TEXT NULL",
        "ALTER TABLE EvaluationResults ADD COLUMN CallReference TEXT NULL",
        "ALTER TABLE EvaluationResults ADD COLUMN CallDate TEXT NULL"
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
