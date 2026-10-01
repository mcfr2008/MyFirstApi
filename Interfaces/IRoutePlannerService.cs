using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IRoutePlannerService
{
    // Best route (plus alternatives) between two points; read-only.
    Task<RoutePlanResponse> PlanAsync(RoutePlanRequest request);
}
