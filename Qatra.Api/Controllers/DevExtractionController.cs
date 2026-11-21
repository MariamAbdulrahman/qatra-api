using Microsoft.AspNetCore.Mvc;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

public record ExtractRequest(string Text);

[ApiController]
[Route("api/dev/extract")]
public class DevExtractionController : ControllerBase
{
    private readonly IAiExtractionService _svc;
    public DevExtractionController(IAiExtractionService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ExtractRequest request, CancellationToken ct)
        => Ok(await _svc.ExtractAsync(request.Text, ct));
}