using Microsoft.AspNetCore.Mvc;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

/// <summary>للاختبار فقط: يعمل في بيئة Development ويرجع 404 في غيرها.</summary>
[ApiController]
[Route("api/dev")]
public class DevExtractionController : ControllerBase
{
    private readonly IAiExtractionService _extraction;
    private readonly IWebHostEnvironment _env;

    public DevExtractionController(IAiExtractionService extraction, IWebHostEnvironment env)
    {
        _extraction = extraction;
        _env = env;
    }

    public record ExtractRequest(string Text);

    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] ExtractRequest request, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var result = await _extraction.ExtractAsync(request.Text, ct);
        return Ok(result);
    }
}