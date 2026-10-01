using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MyFirstApi.Dtos;

public static class PostalCodeRules
{
    // Thai postal codes are 5 digits; other countries 3-10 letters, digits, spaces or '-'.
    private static readonly Regex Thai = new(@"^\d{5}$");
    private static readonly Regex General = new(@"^[A-Za-z0-9 -]{3,10}$");

    public static bool IsValid(string country, string postalCode) =>
        string.Equals(country, "TH", StringComparison.OrdinalIgnoreCase)
            ? Thai.IsMatch(postalCode)
            : General.IsMatch(postalCode);

    public static string Message(string country) =>
        string.Equals(country, "TH", StringComparison.OrdinalIgnoreCase)
            ? "Thai postal codes are 5 digits."
            : "PostalCode must be 3-10 letters, digits, spaces or '-'.";
}

public class ServiceAreaRequest : MasterDataRequest, IValidatableObject
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string Country { get; set; } = "TH";

    // Required when PostalCode is empty (the area is the whole province).
    [StringLength(100)]
    public string? Province { get; set; }

    // One postal code; leave empty for a province-wide area.
    public string? PostalCode { get; set; }

    // Branch, DropPoint, Hub or Warehouse that serves the area.
    [Required]
    public int? StationLocationId { get; set; }

    // A location of type Hub.
    [Required]
    public int? HubLocationId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PostalCode))
        {
            if (string.IsNullOrWhiteSpace(Province))
            {
                yield return new ValidationResult(
                    "Province is required when PostalCode is empty.", [nameof(Province)]);
            }
        }
        else if (!PostalCodeRules.IsValid(Country, PostalCode.Trim()))
        {
            yield return new ValidationResult(PostalCodeRules.Message(Country), [nameof(PostalCode)]);
        }
    }
}

public class ServiceAreaQuery : MasterDataQuery
{
    public string? Country { get; set; }
    // Exact match, case-insensitive.
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public int? StationLocationId { get; set; }
    public int? HubLocationId { get; set; }
}

public class ServiceAreaResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public ReferenceSummary Station { get; set; } = null!;
    public ReferenceSummary Hub { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Find the area for an address (postalCode and/or province) or for an existing location.
public class ServiceAreaResolveQuery : IValidatableObject
{
    public int? LocationId { get; set; }

    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string Country { get; set; } = "TH";

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LocationId == null && string.IsNullOrWhiteSpace(PostalCode) && string.IsNullOrWhiteSpace(Province))
        {
            yield return new ValidationResult("Provide locationId, postalCode or province.");
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<ServiceAreaMatch>))]
public enum ServiceAreaMatch
{
    PostalCode,
    Province
}

public class ServiceAreaResolveResponse
{
    // Which rule matched: the postal-code area, or the province-wide fallback.
    public ServiceAreaMatch MatchedBy { get; set; }
    public ServiceAreaResponse ServiceArea { get; set; } = null!;
}
