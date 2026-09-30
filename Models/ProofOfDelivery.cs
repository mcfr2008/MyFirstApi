using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ReceiverRelation>))]
public enum ReceiverRelation
{
    // The addressee themself.
    Recipient,
    FamilyMember,
    Colleague,
    Reception,
    Security,
    Neighbor,
    Other
}

// Evidence captured at hand-over: who signed, their signature, photos, where and when.
public class ProofOfDelivery
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public string ReceiverName { get; set; } = string.Empty;
    public ReceiverRelation ReceiverRelation { get; set; }
    public Guid SignatureFileId { get; set; }
    public StoredFile SignatureFile { get; set; } = null!;
    public DateTime SignedAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMeters { get; set; }
    public string? DeliveredBy { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Note { get; set; }
    // Items the receiver refused; they get a DELIVERY_FAILED event instead of DELIVERED.
    public List<RefusedItem> RefusedItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public List<ProofOfDeliveryPhoto> Photos { get; set; } = new();
}

public class ProofOfDeliveryPhoto
{
    public int ProofOfDeliveryId { get; set; }
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    public int SortOrder { get; set; }
}

// Stored as JSON inside ProofsOfDelivery.RefusedItems, with camelCase keys
// (same as the API) so it can be queried as "RefusedItems"->0->>'reasonCode'.
public class RefusedItem
{
    [JsonPropertyName("trackedItemId")]
    public int TrackedItemId { get; set; }

    [JsonPropertyName("tagCode")]
    public string TagCode { get; set; } = string.Empty;

    [JsonPropertyName("reasonCode")]
    public string ReasonCode { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
