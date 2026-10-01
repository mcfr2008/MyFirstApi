using System.Text.Json.Serialization;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter<DistanceSource>))]
public enum DistanceSource
{
    // DistanceKm of an active lane between the same two points.
    Lane,
    // Great-circle distance between the coordinates, times a mode adjustment factor.
    GreatCircle,
    // No lane distance and missing coordinates: the leg can't be calculated.
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter<EmissionDataGap>))]
public enum EmissionDataGap
{
    // Some items have no WeightKg; their mass isn't counted.
    ItemsWithoutWeight,
    // A leg has no lane distance and its locations lack coordinates.
    LegDistanceUnknown,
    // No active emission factor for a leg's mode.
    EmissionFactorMissing,
    NoItems
}

// Greenhouse-gas emissions of a shipment (ISO 14083 / GLEC Framework, well-to-wheel):
// per leg, CO2e = tonnes x km x gCO2e/tkm.
public class ShipmentEmissionsResponse
{
    public int ShipmentId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Methodology { get; set; } = string.Empty;
    // Sum over the legs that could be calculated. See IsComplete / DataGaps.
    public decimal TotalCo2eKg { get; set; }
    public decimal TotalTonneKm { get; set; }
    public bool IsComplete { get; set; }
    public List<EmissionDataGap> DataGaps { get; set; } = new();
    public int Pieces { get; set; }
    public int PiecesWithoutWeight { get; set; }
    // Weight of the pieces that have one.
    public decimal MassKg { get; set; }
    public List<LegEmissionsResponse> Legs { get; set; } = new();
}

public class LegEmissionsResponse
{
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public ReferenceSummary Origin { get; set; } = null!;
    public ReferenceSummary Destination { get; set; } = null!;
    public decimal? DistanceKm { get; set; }
    public DistanceSource DistanceSource { get; set; }
    public decimal? TonneKm { get; set; }
    public EmissionFactorSummary? EmissionFactor { get; set; }
    public decimal? Co2eKg { get; set; }
}

public record EmissionFactorSummary(int Id, string Code, decimal GramsCo2ePerTonneKm, string? Source);

// Short form for the public tracking page.
public class EmissionsSummary
{
    public decimal Co2eKg { get; set; }
    public bool IsComplete { get; set; }
}
