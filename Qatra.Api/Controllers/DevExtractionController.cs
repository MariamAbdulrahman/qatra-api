using Microsoft.AspNetCore.Mvc;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/dev/explain")]
public class DevExplainController : ControllerBase
{
    private readonly IAiExplanationService _svc;
    public DevExplainController(IAiExplanationService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ExplanationInput input, CancellationToken ct)
        => Ok(await _svc.ExplainAsync(input, ct));
}