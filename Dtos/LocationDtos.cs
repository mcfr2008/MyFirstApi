using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class LocationRequest : MasterDataRequest, IValidatableObject
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public LocationType? Type { get; set; }

    [StringLength(500)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? SubDistrict { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    // ISO 3166-1 alpha-2, defaults to TH.
    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string? Country { get; set; }

    // UN/LOCODE: 2-letter country + 3-character place code, e.g. THLCH.
    [RegularExpression("^[A-Za-z]{2}[A-Za-z2-9]{3}$", ErrorMessage = "UnLocode must be 5 characters, e.g. THLCH.")]
    public string? UnLocode { get; set; }

    // IATA airport code, e.g. BKK.
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "IataCode must be 3 letters, e.g. BKK.")]
    public string? IataCode { get; set; }

    // IANA time zone, defaults to Asia/Bangkok.
    [StringLength(64)]
    public string? TimeZone { get; set; }

    [Range(-90.0, 90.0)]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public decimal? Longitude { get; set; }

    [StringLength(255)]
    public string? ContactName { get; set; }

    [StringLength(30)]
    public string? ContactPhone { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(TimeZone) && !TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone.Trim(), out _))
        {
            yield return new ValidationResult(
                $"Unknown time zone '{TimeZone}'. Use an IANA name such as Asia/Bangkok.", [nameof(TimeZone)]);
        }
    }
}

public class LocationQuery : MasterDataQuery
{
    public LocationType? Type { get; set; }
    public string? Province { get; set; }
}

public class LocationResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LocationType Type { get; set; }
    public string? AddressLine { get; set; }
    public string? SubDistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? UnLocode { get; set; }
    public string? IataCode { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
