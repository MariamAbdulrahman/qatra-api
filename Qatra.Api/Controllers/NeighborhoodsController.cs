using Microsoft.AspNetCore.Mvc;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NeighborhoodsController : ControllerBase
{
    private readonly INeighborhoodService _service;

    public NeighborhoodsController(INeighborhoodService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<NeighborhoodDto>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }
}