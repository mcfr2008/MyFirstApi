using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class CarrierRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    // Transport modes this carrier operates; legs can only use a carrier for these modes.
    [Required]
    [MinLength(1)]
    public List<TransportMode> Modes { get; set; } = new();

    [RegularExpression("^[A-Za-z]{2,4}$", ErrorMessage = "ScacCode must be 2-4 letters, e.g. MAEU.")]
    public string? ScacCode { get; set; }

    [RegularExpression("^[A-Za-z0-9]{2,3}$", ErrorMessage = "IataCode must be a 2-3 character airline code, e.g. TG.")]
    public string? IataCode { get; set; }

    [StringLength(255)]
    public string? ContactName { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Url]
    [StringLength(500)]
    public string? Website { get; set; }

    // e.g. https://www.maersk.com/tracking/{number}
    [StringLength(500)]
    public string? TrackingUrlTemplate { get; set; }
}

public class CarrierQuery : MasterDataQuery
{
    public TransportMode? Mode { get; set; }
}

public class CarrierResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Raw column value; exposed to clients as the typed Modes list.
    [JsonIgnore]
    public List<string> ModeNames { get; set; } = new();

    public List<TransportMode> Modes => ModeNames.Select(Enum.Parse<TransportMode>).ToList();
    public string? ScacCode { get; set; }
    public string? IataCode { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? TrackingUrlTemplate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
