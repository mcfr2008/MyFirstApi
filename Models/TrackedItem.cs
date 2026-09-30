using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ItemStatus>))]
public enum ItemStatus
{
    Registered,
    InTransit,
    Delivered,
    Returned,
    Lost,
    Damaged,
    // Stopped on purpose, e.g. held by customs.
    OnHold
}

public class TrackedItem
{
    public int Id { get; set; }
    public string TagCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public ItemCategory? Category { get; set; }
    // Where the item is right now.
    public int? CurrentLocationId { get; set; }
    public Location? CurrentLocation { get; set; }
    public int? OwnerPartyId { get; set; }
    public Party? OwnerParty { get; set; }
    // Pallet / container / ULD the item is currently packed in.
    public int? CurrentContainerId { get; set; }
    public Container? CurrentContainer { get; set; }
    // Customs data for international shipments.
    public string? HsCode { get; set; }
    public string? OriginCountry { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Registered;
    public decimal? WeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? DeclaredValue { get; set; }
    // ISO 4217 currency of DeclaredValue.
    public string? Currency { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public bool IsArchived { get; set; }
    // OccurredAt of the newest tracking event; a back-dated event older than this
    // is stored in the history but doesn't change Status/CurrentLocation.
    public DateTime? LastEventAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
