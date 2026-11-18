using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.DTOs;
using Qatra.Api.Interfaces;

namespace Qatra.Api.Services;

public class NeighborhoodService : INeighborhoodService
{
    private readonly AppDbContext _db;

    public NeighborhoodService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<NeighborhoodDto>> GetAllAsync()
    {
        return await _db.Neighborhoods
            .AsNoTracking()
            .OrderBy(n => n.Id)
            .Select(n => new NeighborhoodDto
            {
                Id = n.Id,
                NameAr = n.NameAr,
                Lat = n.Lat,
                Lng = n.Lng
            })
            .ToListAsync();
    }
}