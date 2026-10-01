using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class LaneRequest : MasterDataRequest, IValidatableObject
{
    private static readonly Regex TimeOfDay = new(@"^([01]\d|2[0-3]):[0-5]\d$");

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int? OriginLocationId { get; set; }

    [Required]
    public int? DestinationLocationId { get; set; }

    [Required]
    public TransportMode? Mode { get; set; }

    // Must operate the lane's mode.
    public int? CarrierId { get; set; }

    // Up to 60 days (long sea voyages).
    [Required]
    [Range(1, 60 * 24 * 60)]
    public int? TransitTimeMinutes { get; set; }

    [Range(typeof(decimal), "0", "40000")]
    public decimal? DistanceKm { get; set; }

    // "HH:mm" in the origin location's time zone, e.g. ["08:00", "22:00"]. Empty = on demand.
    [MaxLength(48)]
    public List<string> DepartureTimes { get; set; } = new();

    // Empty = every day.
    public List<Weekday> OperatingDays { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OriginLocationId.HasValue && OriginLocationId == DestinationLocationId)
        {
            yield return new ValidationResult(
                "A lane's origin and destination must differ.", [nameof(DestinationLocationId)]);
        }
        var invalid = DepartureTimes.Where(t => t == null || !TimeOfDay.IsMatch(t.Trim())).ToList();
        if (invalid.Count > 0)
        {
            yield return new ValidationResult(
                $"DepartureTimes must be \"HH:mm\" (00:00-23:59): {string.Join(", ", invalid)}", [nameof(DepartureTimes)]);
        }
    }
}

public class LaneQuery : MasterDataQuery
{
    public int? OriginLocationId { get; set; }
    public int? DestinationLocationId { get; set; }
    // Lanes starting or ending at this location.
    public int? LocationId { get; set; }
    public TransportMode? Mode { get; set; }
    public int? CarrierId { get; set; }
}

public class LaneResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReferenceSummary Origin { get; set; } = null!;
    public ReferenceSummary Destination { get; set; } = null!;
    public TransportMode Mode { get; set; }
    public ReferenceSummary? Carrier { get; set; }
    public int TransitTimeMinutes { get; set; }
    public decimal? DistanceKm { get; set; }
    public List<string> DepartureTimes { get; set; } = new();

    // Raw column value; exposed to clients as the typed OperatingDays list.
    [JsonIgnore]
    public List<string> OperatingDayNames { get; set; } = new();

    public List<Weekday> OperatingDays => OperatingDayNames.Select(Enum.Parse<Weekday>).ToList();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
