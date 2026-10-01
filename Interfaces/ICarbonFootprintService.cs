using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ICarbonFootprintService
{
    // Null when the shipment doesn't exist.
    Task<ShipmentEmissionsResponse?> CalculateAsync(int shipmentId);
}
