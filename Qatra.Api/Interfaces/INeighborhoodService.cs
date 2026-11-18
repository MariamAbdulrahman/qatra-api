using Qatra.Api.DTOs;

namespace Qatra.Api.Interfaces;

public interface INeighborhoodService
{
    Task<List<NeighborhoodDto>> GetAllAsync();
}