using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TransportDocumentType>))]
public enum TransportDocumentType
{
    // Sea
    BillOfLading,
    // Air
    AirWaybill,
    // Rail (CIM / SMGS consignment note)
    RailWaybill,
    // Road (CMR / truck consignment note)
    RoadConsignmentNote,
    CourierWaybill
}

// One segment of a shipment carried by a single mode/vehicle.
public class ShipmentLeg
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    // 1-based order within the shipment.
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public int? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    // Free-text vehicle for outside carriers (vessel name, aircraft type, ...).
    public string? VehicleName { get; set; }
    // Voyage number (sea), flight number (air), train number (rail), trip number (road).
    public string? VoyageNumber { get; set; }
    public int OriginLocationId { get; set; }
    public Location OriginLocation { get; set; } = null!;
    public int DestinationLocationId { get; set; }
    public Location DestinationLocation { get; set; } = null!;
    // ETD / ETA
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? PlannedArrival { get; set; }
    // ATD / ATA
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }
    public TransportDocumentType? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
}
