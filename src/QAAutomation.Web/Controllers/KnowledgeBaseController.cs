using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAAutomation.Web.Models;
using QAAutomation.Web.Services;

namespace QAAutomation.Web.Controllers;

/// <summary>Web UI for managing knowledge base sources and documents.</summary>
[Authorize(Roles = "Admin")]
public class KnowledgeBaseController : Controller
{
    private readonly ApiClient _api;

    /// <summary>Maximum document text size accepted per upload. Larger content is truncated
    /// to avoid exceeding LLM context window limits during RAG retrieval.</summary>
    private const int MaxDocumentChars = 50_000;

    public KnowledgeBaseController(ApiClient api) => _api = api;

    // ── Sources list ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sources = await _api.GetKnowledgeSources();
        return View(sources);
    }

    // ── Create source ─────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create() => View(new KnowledgeSourceViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KnowledgeSourceViewModel model)
    {
        var created = await _api.CreateKnowledgeSource(model);
        if (created == null)
        {
            TempData["Error"] = "Failed to create knowledge source.";
            return View(model);
        }
        TempData["Success"] = $"Knowledge source '{created.Name}' created.";
        return RedirectToAction(nameof(Source), new { id = created.Id });
    }

    // ── View / edit source + docs ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Source(int id)
    {
        var source = await _api.GetKnowledgeSource(id);
        if (source == null) return NotFound();
        var docs = await _api.GetKnowledgeDocuments(id);
        ViewBag.Documents = docs;
        return View(source);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Source(int id, KnowledgeSourceViewModel model)
    {
        var ok = await _api.UpdateKnowledgeSource(id, model);
        TempData[ok ? "Success" : "Error"] = ok ? "Source updated." : "Failed to update source.";
        return RedirectToAction(nameof(Source), new { id });
    }

    // ── Delete source ─────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSource(int id)
    {
        await _api.DeleteKnowledgeSource(id);
        TempData["Success"] = "Knowledge source deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Add document ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDocument(KnowledgeDocumentUploadViewModel model, IFormFile? documentFile)
    {
        // Resolve text content: uploaded file takes precedence
        var content = string.Empty;
        string? fileName = null;

        if (documentFile != null && documentFile.Length > 0)
        {
            using var reader = new System.IO.StreamReader(documentFile.OpenReadStream());
            content = await reader.ReadToEndAsync();
            fileName = documentFile.FileName;
        }
        else if (!string.IsNullOrWhiteSpace(model.TextContent))
        {
            content = model.TextContent;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Please provide document content — either upload a file or paste the text.";
            return RedirectToAction(nameof(Source), new { id = model.SourceId });
        }

        // Truncate to 50 000 chars
        if (content.Length > MaxDocumentChars) content = content[..MaxDocumentChars] + "\n[CONTENT TRUNCATED]";

        var dto = new
        {
            sourceId = model.SourceId,
            title = model.Title,
            fileName,
            content,
            tags = model.Tags
        };

        var ok = await _api.AddKnowledgeDocument(dto);
        TempData[ok ? "Success" : "Error"] = ok ? $"Document '{model.Title}' added." : "Failed to add document.";
        return RedirectToAction(nameof(Source), new { id = model.SourceId });
    }

    // ── Delete document ───────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id, int sourceId)
    {
        await _api.DeleteKnowledgeDocument(id);
        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(Source), new { id = sourceId });
    }
}
