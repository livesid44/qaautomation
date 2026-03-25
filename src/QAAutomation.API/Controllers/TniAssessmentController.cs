using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>
/// Manages LLM-powered training content generation, MCQ assessments, and the TNI dashboard.
///
/// Endpoints:
///   POST /api/tni/{planId}/generate      — generate training content + MCQ via LLM
///   GET  /api/tni/{planId}/assessment    — retrieve assessment (agent view: no correct answers)
///   POST /api/tni/{planId}/attempt       — submit answers, get result
///   GET  /api/tni/{planId}/attempts      — list all attempts for a plan
///   GET  /api/tni/dashboard              — summary stats (pending / passed / failed)
/// </summary>
[ApiController]
[Route("api/tni")]
public class TniAssessmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TniGenerationService _generator;
    private readonly ILogger<TniAssessmentController> _logger;

    public TniAssessmentController(
        AppDbContext db,
        TniGenerationService generator,
        ILogger<TniAssessmentController> logger)
    {
        _db = db;
        _generator = generator;
        _logger = logger;
    }

    // ── POST /api/tni/{planId}/generate ──────────────────────────────────────

    /// <summary>
    /// Generates LLM training content and MCQ assessment for the specified training plan.
    /// Overwrites any previously generated content for the same plan.
    /// </summary>
    [HttpPost("{planId}/generate")]
    [ProducesResponseType(typeof(TniAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TniAssessmentDto>> Generate(
        int planId,
        [FromBody] TniGenerateRequestDto req,
        CancellationToken ct)
    {
        var plan = await _db.TrainingPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);

        if (plan is null) return NotFound($"Training plan {planId} not found.");

        // Collect target areas from items (these are the "failed" parameter labels)
        var areas = plan.Items
            .Select(i => i.TargetArea)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct()
            .ToList();

        // Fall back to plan title when items have no target areas
        if (areas.Count == 0)
            areas.Add(plan.Title);

        try
        {
            var (content, json) = await _generator.GenerateAsync(
                areas, plan.AgentName, plan.Title, req.QuestionCount, ct);

            plan.LlmTrainingContent = content;
            plan.AssessmentJson = json;
            plan.ContentGeneratedAt = DateTime.UtcNow;
            plan.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(BuildAssessmentDto(plan, agentUsername: null, includeAnswers: true));
    }

    // ── GET /api/tni/{planId}/assessment ─────────────────────────────────────

    /// <summary>
    /// Returns the assessment for the agent to take.
    /// Correct answers are excluded from the response (returned only after submission).
    /// Optionally include ?agentUsername= to also return the most recent attempt for that agent.
    /// </summary>
    [HttpGet("{planId}/assessment")]
    [ProducesResponseType(typeof(TniAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TniAssessmentDto>> GetAssessment(
        int planId,
        [FromQuery] string? agentUsername = null,
        CancellationToken ct = default)
    {
        var plan = await _db.TrainingPlans
            .Include(p => p.AssessmentAttempts)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);

        if (plan is null) return NotFound($"Training plan {planId} not found.");
        if (string.IsNullOrWhiteSpace(plan.AssessmentJson))
            return BadRequest("No assessment has been generated for this plan yet. Call POST /generate first.");

        return Ok(BuildAssessmentDto(plan, agentUsername, includeAnswers: false));
    }

    // ── POST /api/tni/{planId}/attempt ───────────────────────────────────────

    /// <summary>
    /// Submits the agent's answers for the assessment. Calculates the score and persists the attempt.
    /// Returns full feedback including correct answers and explanations.
    /// </summary>
    [HttpPost("{planId}/attempt")]
    [ProducesResponseType(typeof(TniAttemptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TniAttemptResultDto>> SubmitAttempt(
        int planId,
        [FromBody] TniSubmitAttemptDto dto,
        CancellationToken ct)
    {
        var plan = await _db.TrainingPlans
            .Include(p => p.AssessmentAttempts)
            .FirstOrDefaultAsync(p => p.Id == planId, ct);

        if (plan is null) return NotFound($"Training plan {planId} not found.");
        if (string.IsNullOrWhiteSpace(plan.AssessmentJson))
            return BadRequest("No assessment has been generated for this plan yet.");

        List<AssessmentQuestion> questions;
        try { questions = ParseQuestions(plan.AssessmentJson); }
        catch { return BadRequest("Assessment data is corrupt. Please regenerate."); }

        if (dto.Answers.Count != questions.Count)
            return BadRequest($"Expected {questions.Count} answers but received {dto.Answers.Count}.");

        // Score the attempt
        int correct = 0;
        var feedback = new List<TniQuestionFeedbackDto>();
        for (int i = 0; i < questions.Count; i++)
        {
            var isCorrect = dto.Answers[i] == questions[i].CorrectIndex;
            if (isCorrect) correct++;
            feedback.Add(new TniQuestionFeedbackDto
            {
                Index = i,
                Question = questions[i].Question,
                AgentAnswer = dto.Answers[i],
                CorrectAnswer = questions[i].CorrectIndex,
                IsCorrect = isCorrect,
                Explanation = questions[i].Explanation
            });
        }

        double scorePercent = questions.Count > 0 ? Math.Round(correct * 100.0 / questions.Count, 1) : 0;
        var result = scorePercent >= plan.AssessmentPassMark ? "Pass" : "Fail";

        var attempt = new TniAssessmentAttempt
        {
            TrainingPlanId = planId,
            AgentUsername = dto.AgentUsername,
            AnswersJson = JsonSerializer.Serialize(dto.Answers),
            CorrectAnswers = correct,
            TotalQuestions = questions.Count,
            ScorePercent = scorePercent,
            Result = result,
            AttemptedAt = DateTime.UtcNow
        };

        _db.TniAssessmentAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        return Ok(new TniAttemptResultDto
        {
            AttemptId = attempt.Id,
            TrainingPlanId = planId,
            AgentUsername = dto.AgentUsername,
            CorrectAnswers = correct,
            TotalQuestions = questions.Count,
            ScorePercent = scorePercent,
            Result = result,
            AttemptedAt = attempt.AttemptedAt,
            Feedback = feedback
        });
    }

    // ── GET /api/tni/{planId}/attempts ───────────────────────────────────────

    /// <summary>Lists all assessment attempts for a training plan.</summary>
    [HttpGet("{planId}/attempts")]
    [ProducesResponseType(typeof(IEnumerable<TniAttemptResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TniAttemptResultDto>>> GetAttempts(
        int planId,
        [FromQuery] string? agentUsername = null,
        CancellationToken ct = default)
    {
        var query = _db.TniAssessmentAttempts
            .Where(a => a.TrainingPlanId == planId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(agentUsername))
            query = query.Where(a => a.AgentUsername == agentUsername);

        var attempts = await query.OrderByDescending(a => a.AttemptedAt).ToListAsync(ct);
        return Ok(attempts.Select(a => new TniAttemptResultDto
        {
            AttemptId = a.Id,
            TrainingPlanId = a.TrainingPlanId,
            AgentUsername = a.AgentUsername,
            CorrectAnswers = a.CorrectAnswers,
            TotalQuestions = a.TotalQuestions,
            ScorePercent = a.ScorePercent,
            Result = a.Result,
            AttemptedAt = a.AttemptedAt,
            Feedback = new() // feedback not stored after the fact
        }));
    }

    // ── GET /api/tni/dashboard ────────────────────────────────────────────────

    /// <summary>
    /// Returns a summary of all TNI plans with their assessment status.
    /// Optional filter: ?projectId= or ?agentUsername=
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(TniDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TniDashboardDto>> Dashboard(
        [FromQuery] int? projectId = null,
        [FromQuery] string? agentUsername = null,
        CancellationToken ct = default)
    {
        var query = _db.TrainingPlans
            .Include(p => p.AssessmentAttempts)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(p => p.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(agentUsername))
            query = query.Where(p => p.AgentUsername == agentUsername);

        var plans = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

        var items = plans.Select(p =>
        {
            var hasAssessment = !string.IsNullOrWhiteSpace(p.AssessmentJson);
            // Latest attempt for this plan (any agent)
            var latestAttempt = p.AssessmentAttempts
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefault();

            string assessmentStatus = "Pending";
            if (hasAssessment && latestAttempt != null)
                assessmentStatus = latestAttempt.Result; // "Pass" or "Fail"

            return new TniDashboardItemDto
            {
                PlanId = p.Id,
                PlanTitle = p.Title,
                AgentName = p.AgentName,
                AgentUsername = p.AgentUsername,
                PlanStatus = p.Status,
                HasAssessment = hasAssessment,
                AssessmentStatus = assessmentStatus,
                LatestScore = latestAttempt?.ScorePercent,
                LatestAttemptAt = latestAttempt?.AttemptedAt,
                AttemptCount = p.AssessmentAttempts.Count,
                CreatedAt = p.CreatedAt,
                ProjectId = p.ProjectId
            };
        }).ToList();

        return Ok(new TniDashboardDto
        {
            TotalPlans = items.Count,
            PlansWithAssessment = items.Count(i => i.HasAssessment),
            PendingAssessments = items.Count(i => i.AssessmentStatus == "Pending"),
            PassedAssessments = items.Count(i => i.AssessmentStatus == "Pass"),
            FailedAssessments = items.Count(i => i.AssessmentStatus == "Fail"),
            Items = items
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private record AssessmentQuestion(string Question, List<string> Options, int CorrectIndex, string Explanation);

    private static List<AssessmentQuestion> ParseQuestions(string json)
    {
        var arr = JsonSerializer.Deserialize<JsonElement>(json);
        var list = new List<AssessmentQuestion>();
        foreach (var el in arr.EnumerateArray())
        {
            var opts = new List<string>();
            if (el.TryGetProperty("options", out var optsEl))
                foreach (var o in optsEl.EnumerateArray())
                    opts.Add(o.GetString() ?? "");

            list.Add(new AssessmentQuestion(
                el.TryGetProperty("question", out var qEl) ? qEl.GetString() ?? "" : "",
                opts,
                el.TryGetProperty("correctIndex", out var ciEl) ? ciEl.GetInt32() : 0,
                el.TryGetProperty("explanation", out var exEl) ? exEl.GetString() ?? "" : ""));
        }
        return list;
    }

    private static TniAssessmentDto BuildAssessmentDto(TrainingPlan plan, string? agentUsername, bool includeAnswers)
    {
        var questions = new List<TniAssessmentQuestionDto>();

        if (!string.IsNullOrWhiteSpace(plan.AssessmentJson))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement>(plan.AssessmentJson);
                int idx = 0;
                foreach (var el in arr.EnumerateArray())
                {
                    var opts = new List<string>();
                    if (el.TryGetProperty("options", out var optsEl))
                        foreach (var o in optsEl.EnumerateArray())
                            opts.Add(o.GetString() ?? "");

                    int correctIdx = el.TryGetProperty("correctIndex", out var ciEl) ? ciEl.GetInt32() : 0;
                    questions.Add(new TniAssessmentQuestionDto
                    {
                        Index = idx++,
                        Question = el.TryGetProperty("question", out var qEl) ? qEl.GetString() ?? "" : "",
                        Options = opts,
                        CorrectIndex = includeAnswers ? correctIdx : null,
                        Explanation = includeAnswers && el.TryGetProperty("explanation", out var exEl) ? exEl.GetString() : null
                    });
                }
            }
            catch { /* malformed JSON — return empty list */ }
        }

        TniAttemptResultDto? latestAttempt = null;
        if (!string.IsNullOrWhiteSpace(agentUsername))
        {
            var attempt = plan.AssessmentAttempts
                .Where(a => a.AgentUsername == agentUsername)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefault();
            if (attempt != null)
            {
                latestAttempt = new TniAttemptResultDto
                {
                    AttemptId = attempt.Id,
                    TrainingPlanId = attempt.TrainingPlanId,
                    AgentUsername = attempt.AgentUsername,
                    CorrectAnswers = attempt.CorrectAnswers,
                    TotalQuestions = attempt.TotalQuestions,
                    ScorePercent = attempt.ScorePercent,
                    Result = attempt.Result,
                    AttemptedAt = attempt.AttemptedAt,
                    Feedback = new()
                };
            }
        }

        return new TniAssessmentDto
        {
            TrainingPlanId = plan.Id,
            TrainingPlanTitle = plan.Title,
            TrainingContent = plan.LlmTrainingContent ?? string.Empty,
            Questions = questions,
            PassMark = plan.AssessmentPassMark,
            GeneratedAt = plan.ContentGeneratedAt,
            LatestAttempt = latestAttempt
        };
    }
}
