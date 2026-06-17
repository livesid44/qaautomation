using Microsoft.AspNetCore.Mvc;

namespace QAAutomation.API.Controllers;

/// <summary>
/// Lightweight connectivity check used by the Web layer's diagnostic ping.
/// GET /api/ping  — returns 200 OK immediately without touching the database.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    /// <summary>Returns 200 OK to confirm the API is reachable.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { ok = true, utc = DateTime.UtcNow });
}
