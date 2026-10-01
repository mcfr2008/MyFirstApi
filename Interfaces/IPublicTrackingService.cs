using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IPublicTrackingService
{
    // Null when there is no shipment with this tracking number.
    Task<PublicTrackingResponse?> TrackAsync(string trackingNumber);
}
