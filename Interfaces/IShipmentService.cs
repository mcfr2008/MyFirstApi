using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IShipmentService
{
    Task<PagedResult<ShipmentResponse>> GetShipmentsAsync(ShipmentQuery query);
    Task<ShipmentResponse?> GetShipmentByIdAsync(int id);
    Task<ShipmentResponse?> GetShipmentByTrackingNumberAsync(string trackingNumber);
    Task<ShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request);
    Task<ShipmentResponse?> UpdateShipmentAsync(int id, UpdateShipmentRequest request);
    Task<ShipmentResponse?> UpdateLegAsync(int id, int legId, UpdateShipmentLegRequest request);

    // Plans a route (see IRoutePlannerService) and replaces the legs; Planned shipments only.
    Task<ShipmentRouteResponse?> RouteAsync(int id, RouteShipmentRequest request);

    // Planned route vs. where the items were scanned (misroutes).
    Task<ShipmentRouteCheckResponse?> GetRouteCheckAsync(int id);

    Task<IReadOnlyList<TrackedItemResponse>?> GetItemsAsync(int id);
    Task<ShipmentResponse?> AddItemsAsync(int id, ShipmentItemsRequest request);
    Task<ShipmentResponse?> RemoveItemAsync(int id, int trackedItemId);

    // Each of these records tracking events for every item in the shipment.
    Task<ShipmentResponse?> DepartLegAsync(int id, int legId, LegMovementRequest request);
    Task<ShipmentResponse?> ArriveLegAsync(int id, int legId, LegMovementRequest request);
    Task<ShipmentResponse?> UpdateCustomsAsync(int id, CustomsUpdateRequest request);
    Task<EventsRecordedResponse?> RecordEventAsync(int id, EventDetails request);
    Task<ShipmentResponse?> DeliverAsync(int id, DeliverShipmentRequest request);

    // Receiver signature (+ photos, GPS): stores the evidence and delivers the shipment.
    Task<ProofOfDeliveryResponse?> CreateProofOfDeliveryAsync(int id, ProofOfDeliveryRequest request);
    Task<ProofOfDeliveryResponse?> GetProofOfDeliveryAsync(int id);

    Task<ShipmentResponse?> CancelAsync(int id);
}
