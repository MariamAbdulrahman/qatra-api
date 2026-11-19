using Microsoft.AspNetCore.Mvc;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsController(IReportService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ReportCreatedDto>> Create(CreateReportDto dto)
    {
        var created = await _service.CreateAsync(dto);

        if (created is null)
        {
            ModelState.AddModelError(nameof(dto.NeighborhoodId), "الحي غير موجود.");
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetStatus), new { id = created.Id }, created);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportStatusDto>> GetStatus(int id)
    {
        var result = await _service.GetStatusAsync(id);
        return result is null ? NotFound() : Ok(result);
    }
}