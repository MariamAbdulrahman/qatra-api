using Microsoft.AspNetCore.Mvc;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/predictions")]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictions;

    public PredictionsController(IPredictionService predictions) => _predictions = predictions;

    /// <summary>تنبؤ دورة المياه لحي معيّن.</summary>
    [HttpGet("{neighborhoodId:int}")]
    [ProducesResponseType(typeof(PredictionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int neighborhoodId, CancellationToken ct)
    {
        var result = await _predictions.GetPredictionAsync(neighborhoodId, ct);
        return result is null
            ? NotFound(new { message = $"Neighborhood {neighborhoodId} not found." })
            : Ok(result);
    }
}