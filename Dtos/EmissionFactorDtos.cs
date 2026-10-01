using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class EmissionFactorRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TransportMode? Mode { get; set; }

    // Empty = the mode's default; set = this carrier's own value (must run the mode).
    public int? CarrierId { get; set; }

    // Well-to-wheel grams CO2e per tonne-km.
    [Required]
    [Range(typeof(decimal), "0", "100000")]
    public decimal? GramsCo2ePerTonneKm { get; set; }

    [StringLength(500)]
    public string? Source { get; set; }
}

public class EmissionFactorQuery : MasterDataQuery
{
    public TransportMode? Mode { get; set; }
    public int? CarrierId { get; set; }
}

public class EmissionFactorResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public ReferenceSummary? Carrier { get; set; }
    public decimal GramsCo2ePerTonneKm { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
