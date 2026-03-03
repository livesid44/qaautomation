using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using OpenAI.Chat;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>
/// Translates a free-text question into a tenant-scoped SQL query using the
/// tenant's configured LLM, executes it against the SQLite database, and
/// generates narrative insights from the result set.
///
/// Tenant isolation is enforced server-side: the LLM's SELECT is always wrapped
/// inside CTEs that restrict every table to the requesting project (tenant).
/// </summary>
public class InsightsChatService
{
    private readonly IAiConfigService _aiConfig;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<InsightsChatService> _logger;

    public InsightsChatService(
        IAiConfigService aiConfig,
        IConfiguration config,
        IWebHostEnvironment env,
        ILogger<InsightsChatService> logger)
    {
        _aiConfig = aiConfig;
        _config = config;
        _env = env;
        _logger = logger;
    }

    // ── Public entry point ───────────────────────────────────────────────────

    public async Task<InsightsChatResponseDto> AskAsync(InsightsChatRequestDto req, CancellationToken ct = default)
    {
        var cfg = await _aiConfig.GetAsync();

        if (string.IsNullOrWhiteSpace(cfg.LlmEndpoint) || string.IsNullOrWhiteSpace(cfg.LlmApiKey))
            return Error(req.Question, "LLM is not configured. Please configure AI Settings first.");

        // Clamp to a safe non-negative integer; this value is interpolated directly
        // into the tenant-scoped CTE SQL, so it must never contain non-numeric characters.
        var projectId = Math.Max(0, req.ProjectId ?? 0);

        // ── Step 1: NL → SQL via LLM ─────────────────────────────────────────
        string rawLlmSql;
        try
        {
            var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
            var client = AzureOpenAIHelper.CreateClient(ep, cfg.LlmApiKey, dep);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(BuildSqlSystemPrompt(projectId)),
                new UserChatMessage(req.Question)
            };

            var opts = new ChatCompletionOptions { Temperature = 0.0f, MaxOutputTokenCount = 1200 };
            var resp = await client.CompleteChatAsync(messages, opts, ct);
            rawLlmSql = resp.Value.Content[0].Text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM SQL generation failed");
            return Error(req.Question, $"AI error during SQL generation: {ex.Message}");
        }

        // ── Step 2: Extract & validate the SELECT ────────────────────────────
        var selectSql = ExtractSelectSql(rawLlmSql);
        if (string.IsNullOrWhiteSpace(selectSql))
            return new InsightsChatResponseDto
            {
                Question = req.Question,
                Sql = rawLlmSql,
                Error = "The AI did not return a valid SELECT statement. Please rephrase your question."
            };

        // ── Step 3: Wrap in tenant-scoped CTEs and execute ───────────────────
        var fullSql = BuildTenantScopedQuery(selectSql, projectId);
        List<string> columns;
        List<List<object?>> rows;
        try
        {
            (columns, rows) = ExecuteQuery(fullSql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query execution failed. SQL:\n{Sql}", fullSql);
            return new InsightsChatResponseDto
            {
                Question = req.Question,
                Sql = selectSql,
                Error = $"Query execution error: {ex.Message}"
            };
        }

        // ── Step 4: Generate insights from the result set ────────────────────
        var insights = await GenerateInsightsAsync(cfg, req.Question, columns, rows, ct);

        return new InsightsChatResponseDto
        {
            Question = req.Question,
            Sql = selectSql,
            Columns = columns,
            Rows = rows,
            Insights = insights,
            RowCount = rows.Count
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildSqlSystemPrompt(int projectId) => $"""
        You are a SQL expert for an AI-driven QA (quality assurance) call-centre platform.
        Your job is to convert a natural-language question into a single SQLite SELECT statement.

        The database is already filtered for the current tenant (ProjectId = {projectId}) via
        these pre-defined CTEs you MUST use instead of the raw tables:

          TenantLobs      — lines of business   (Id, ProjectId, Name, Description, IsActive, CreatedAt)
          TenantForms     — evaluation forms     (Id, LobId, Name, Description, IsActive, CreatedAt, UpdatedAt)
          TenantResults   — audit results        (Id, FormId, EvaluatedBy, EvaluatedAt, AgentName,
                                                  CallReference, CallDate, CallDurationSeconds, Notes, IsAiGenerated)
          TenantScores    — individual scores    (Id, ResultId, FieldId, NumericValue, TextValue, BoolValue)
          TenantTraining  — training plans       (Id, ProjectId, Title, AgentName, TrainerName, Status,
                                                  DueDate, CreatedAt, UpdatedAt)
          TenantPipeline  — call pipeline jobs   (Id, ProjectId, Name, Status, CreatedAt, CompletedAt,
                                                  TotalItems, CompletedItems, FailedItems)

        Additional tables you may JOIN as needed (not tenant-scoped, join on IDs):
          FormFields   (Id, SectionId, Label, FieldType, MaxRating, DisplayOrder, IsActive)
          FormSections (Id, FormId, Title, DisplayOrder)
          AppUsers     (Id, Username, Email, Role, IsActive)
          HumanReviewItems (Id, EvaluationResultId, ReviewedBy, Status, Notes, ReviewedAt)

        Rules:
        1. Write ONLY a SELECT statement — never INSERT / UPDATE / DELETE / DROP / CREATE / PRAGMA.
        2. Always query from the Tenant* CTEs, not the raw underlying tables.
        3. If the question asks for trends over time, GROUP BY strftime('%Y-%m-%d', ...) on date columns.
        4. Use clear readable column aliases (e.g. "Agent Name", "Avg Score %", "Audit Count").
        5. For percentage scores: ROUND(SUM(NumericValue) * 100.0 / SUM(MaxRating), 1) from TenantScores
           joined to FormFields on FieldId — MaxRating > 0 only.
        6. Return ONLY raw SQL — no markdown fences, no explanation, no comments.
        """;

    private static string ExtractSelectSql(string llmOutput)
    {
        // Strip markdown code fences if present
        var cleaned = Regex.Replace(llmOutput, @"```[\w]*\n?", "", RegexOptions.Multiline)
                           .Replace("```", "").Trim();

        var selectIdx = cleaned.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        if (selectIdx < 0) return string.Empty;

        var sql = cleaned[selectIdx..].Trim();

        // Reject any dangerous statements
        var upper = sql.ToUpperInvariant();
        if (Regex.IsMatch(upper, @"\b(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|ATTACH|DETACH|PRAGMA|VACUUM|REINDEX)\b"))
            return string.Empty;

        return sql;
    }

    /// <summary>
    /// Prepends server-enforced tenant CTEs so the LLM's SELECT can only see
    /// data belonging to <paramref name="projectId"/>.
    /// The <paramref name="projectId"/> value is a C# <c>int</c> and is validated
    /// to be non-negative before reaching this method, so string interpolation is safe.
    /// </summary>
    private static string BuildTenantScopedQuery(string selectSql, int projectId)
    {
        // Guard: projectId must be a non-negative integer — no user-controlled string reaches here.
        if (projectId < 0) projectId = 0;
        // Outer LIMIT caps runaway queries; LLM-generated LIMIT inside is still respected
        return $"""
            WITH
            TenantLobs AS (
                SELECT * FROM Lobs WHERE ProjectId = {projectId}
            ),
            TenantForms AS (
                SELECT ef.* FROM EvaluationForms ef
                WHERE ef.LobId IN (SELECT Id FROM TenantLobs)
            ),
            TenantResults AS (
                SELECT er.* FROM EvaluationResults er
                WHERE er.FormId IN (SELECT Id FROM TenantForms)
            ),
            TenantScores AS (
                SELECT es.* FROM EvaluationScores es
                WHERE es.ResultId IN (SELECT Id FROM TenantResults)
            ),
            TenantTraining AS (
                SELECT * FROM TrainingPlans WHERE ProjectId = {projectId}
            ),
            TenantPipeline AS (
                SELECT * FROM CallPipelineJobs WHERE ProjectId = {projectId}
            ),
            UserQuery AS (
                {selectSql}
            )
            SELECT * FROM UserQuery
            LIMIT 500
            """;
    }

    private (List<string> columns, List<List<object?>> rows) ExecuteQuery(string sql)
    {
        var connStr = _config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connStr))
            connStr = $"Data Source={Path.Combine(_env.ContentRootPath, "qa_automation.db")}";

        // Ensure read-only access; append ;Mode=ReadOnly if not already present
        if (!connStr.Contains("Mode=", StringComparison.OrdinalIgnoreCase))
            connStr = connStr.TrimEnd(';') + ";Mode=ReadOnly";

        var columns = new List<string>();
        var rows = new List<List<object?>>();

        using var conn = new SqliteConnection(connStr);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = cmd.ExecuteReader();
        for (var i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        while (reader.Read())
        {
            var row = new List<object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
            rows.Add(row);
        }

        return (columns, rows);
    }

    private async Task<string> GenerateInsightsAsync(
        AiConfig cfg, string question,
        List<string> columns, List<List<object?>> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0) return "No data was found for this query.";

        try
        {
            var (ep, dep) = AzureOpenAIHelper.NormalizeEndpoint(cfg.LlmEndpoint, cfg.LlmDeployment);
            var client = AzureOpenAIHelper.CreateClient(ep, cfg.LlmApiKey, dep);

            var sb = new StringBuilder();
            sb.AppendLine($"Question: {question}");
            sb.AppendLine($"Columns: {string.Join(", ", columns)}");
            sb.AppendLine($"Total rows returned: {rows.Count}");
            sb.AppendLine("Data sample (first 25 rows):");
            foreach (var row in rows.Take(25))
                sb.AppendLine(string.Join(" | ", row.Select(v => v?.ToString() ?? "—")));

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You are a QA analytics expert for a call-centre quality team. " +
                    "Analyse the provided query results and return 3–5 concise, actionable bullet-point insights. " +
                    "Highlight patterns, anomalies, top/bottom performers, and specific recommendations. " +
                    "Be brief — each bullet should be one or two sentences."),
                new UserChatMessage(sb.ToString())
            };

            var opts = new ChatCompletionOptions { Temperature = 0.3f, MaxOutputTokenCount = 700 };
            var resp = await client.CompleteChatAsync(messages, opts, ct);
            return resp.Value.Content[0].Text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Insights generation failed");
            return string.Empty;
        }
    }

    private static InsightsChatResponseDto Error(string question, string message) =>
        new() { Question = question, Error = message };
}
