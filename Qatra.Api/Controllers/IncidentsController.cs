using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;
using Qatra.Api.Models.Enums;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IRankingAndWarningService _ranking;
    private readonly AppDbContext _db;

    public IncidentsController(IRankingAndWarningService ranking, AppDbContext db)
    {
        _ranking = ranking;
        _db = db;
    }

    /// <summary>الحوادث المفتوحة مرتّبة بالأولوية مع الدرجة والتفسير.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RankedIncident>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _ranking.GetRankedIncidentsAsync(ct));

    /// <summary>تفاصيل حادثة واحدة.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RankedIncident), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var all = await _ranking.GetRankedIncidentsAsync(ct);
        var incident = all.FirstOrDefault(i => i.IncidentId == id);

        // إن لم توجد في المفتوحة، نتحقق إن كانت موجودة أصلاً (مغلقة/منتهية)
        if (incident is null)
        {
            var exists = await _db.Incidents.AnyAsync(i => i.Id == id, ct);
            return exists
                ? NotFound(new ProblemDetails { Title = "الحادثة مغلقة أو منتهية", Status = 404 })
                : NotFound(new ProblemDetails { Title = "الحادثة غير موجودة", Status = 404 });
        }

        return Ok(incident);
    }

    /// <summary>تحديث حالة الحادثة (للوحة المدير).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateIncidentStatusRequest request, CancellationToken ct)
    {
        var incident = await _db.Incidents.FindAsync([id], ct);
        if (incident is null)
            return NotFound(new ProblemDetails { Title = "الحادثة غير موجودة", Status = 404 });

        incident.Status = request.Status;

        if (request.Status == IncidentStatus.Resolved && incident.ResolvedAt is null)
            incident.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
