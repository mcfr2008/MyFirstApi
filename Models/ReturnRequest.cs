using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

// Stored statuses. Received / Cancelled after approval are derived from the return
// shipment (see ReturnRequestResponse.Status), so they can't drift apart.
[JsonConverter(typeof(JsonStringEnumConverter<ReturnRequestStatus>))]
public enum ReturnRequestStatus
{
    Requested,
    Approved,
    Rejected,
    Cancelled,
    // Derived only: the approved return shipment was delivered back to the sender.
    Received
}

// A customer return (RMA) of delivered items from one shipment.
public class ReturnRequest
{
    public int Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Requested;
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionNote { get; set; }
    public int? ReturnShipmentId { get; set; }
    public Shipment? ReturnShipment { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ReturnRequestItem> Items { get; set; } = new();
}

public class ReturnRequestItem
{
    public int ReturnRequestId { get; set; }
    public ReturnRequest ReturnRequest { get; set; } = null!;
    public int TrackedItemId { get; set; }
    public TrackedItem TrackedItem { get; set; } = null!;
    public int ReasonCodeId { get; set; }
    public ReasonCode ReasonCode { get; set; } = null!;
    public string? Note { get; set; }
}
