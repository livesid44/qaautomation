using Microsoft.AspNetCore.Mvc;
using QAAutomation.API.DTOs;
using QAAutomation.API.Services;

namespace QAAutomation.API.Controllers;

/// <summary>Controller for managing knowledge base sources and documents.</summary>
[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseService _svc;

    public KnowledgeBaseController(IKnowledgeBaseService svc) => _svc = svc;

    // ── Sources ───────────────────────────────────────────────────────────────

    [HttpGet("sources")]
    [ProducesResponseType(typeof(List<KnowledgeSourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<KnowledgeSourceDto>>> GetSources() =>
        Ok(await _svc.GetSourcesAsync());

    [HttpGet("sources/{id:int}")]
    [ProducesResponseType(typeof(KnowledgeSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeSourceDto>> GetSource(int id)
    {
        var src = await _svc.GetSourceAsync(id);
        return src == null ? NotFound() : Ok(src);
    }

    [HttpPost("sources")]
    [ProducesResponseType(typeof(KnowledgeSourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeSourceDto>> CreateSource([FromBody] KnowledgeSourceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");
        var created = await _svc.CreateSourceAsync(dto);
        return CreatedAtAction(nameof(GetSource), new { id = created.Id }, created);
    }

    [HttpPut("sources/{id:int}")]
    [ProducesResponseType(typeof(KnowledgeSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeSourceDto>> UpdateSource(int id, [FromBody] KnowledgeSourceDto dto)
    {
        var updated = await _svc.UpdateSourceAsync(id, dto);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("sources/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSource(int id) =>
        await _svc.DeleteSourceAsync(id) ? NoContent() : NotFound();

    // ── Documents ─────────────────────────────────────────────────────────────

    [HttpGet("documents")]
    [ProducesResponseType(typeof(List<KnowledgeDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<KnowledgeDocumentDto>>> GetDocuments([FromQuery] int? sourceId = null) =>
        Ok(await _svc.GetDocumentsAsync(sourceId));

    [HttpPost("documents")]
    [ProducesResponseType(typeof(KnowledgeDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeDocumentDto>> AddDocument([FromBody] KnowledgeDocumentUploadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest("Content is required.");
        var doc = await _svc.AddDocumentAsync(dto);
        return StatusCode(StatusCodes.Status201Created, doc);
    }

    [HttpDelete("documents/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(int id) =>
        await _svc.DeleteDocumentAsync(id) ? NoContent() : NotFound();

    // ── RAG search (for testing retrieval) ───────────────────────────────────

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> Search([FromQuery] string q, [FromQuery] int topK = 3) =>
        Ok(await _svc.RetrieveAsync(q, topK));
}
