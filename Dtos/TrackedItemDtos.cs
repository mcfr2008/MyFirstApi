using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

// Fields shared by create and update requests.
public abstract class TrackedItemFields : IValidatableObject
{
    public const int MaxAttributes = 50;

    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
    public int? OwnerPartyId { get; set; }

    // Customs: Harmonized System code, e.g. 8471.30.
    [RegularExpression(@"^[0-9.]{4,12}$", ErrorMessage = "HsCode must be 4-12 digits (dots allowed), e.g. 8471.30.")]
    public string? HsCode { get; set; }

    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string? OriginCountry { get; set; }

    [Range(typeof(decimal), "0", "1000000")]
    public decimal? WeightKg { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? LengthCm { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? WidthCm { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? HeightCm { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? DeclaredValue { get; set; }

    // Currency of DeclaredValue; defaults to THB when a value is given.
    [RegularExpression(IsoRules.CurrencyPattern, ErrorMessage = IsoRules.CurrencyMessage)]
    public string? Currency { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Attributes == null) yield break;

        if (Attributes.Count > MaxAttributes)
        {
            yield return new ValidationResult(
                $"At most {MaxAttributes} attributes are allowed.", [nameof(Attributes)]);
        }

        foreach (var (key, value) in Attributes)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            {
                yield return new ValidationResult(
                    "Attribute keys must be 1-100 characters.", [nameof(Attributes)]);
            }
            if (value != null && value.Length > 1000)
            {
                yield return new ValidationResult(
                    $"Attribute '{key}' must be at most 1000 characters.", [nameof(Attributes)]);
            }
        }
    }
}

public class CreateTrackedItemRequest : TrackedItemFields
{
    // Optional: a code is generated (TT-XXXXXXXXXX) when the item has no physical tag yet.
    [RegularExpression(TagCodeRules.Pattern, ErrorMessage = TagCodeRules.ErrorMessage)]
    public string? TagCode { get; set; }

    // Where the item is registered. Afterwards the location only changes
    // through tracking events, so it is not part of the update request.
    public int? CurrentLocationId { get; set; }
}

public class UpdateTrackedItemRequest : TrackedItemFields
{
    // Required on update so a replaced/relabelled tag can be recorded.
    [Required]
    [RegularExpression(TagCodeRules.Pattern, ErrorMessage = TagCodeRules.ErrorMessage)]
    public string TagCode { get; set; } = string.Empty;
}

public static class TagCodeRules
{
    public const string Pattern = @"^[A-Za-z0-9._-]{3,64}$";
    public const string ErrorMessage = "TagCode must be 3-64 characters: letters, digits, '.', '_' or '-'.";
}

public class BulkCreateTrackedItemsRequest
{
    public const int MaxItems = 500;

    [Required]
    [MinLength(1)]
    [MaxLength(MaxItems)]
    public List<CreateTrackedItemRequest> Items { get; set; } = new();
}

public class TrackedItemQuery : PagedQuery
{
    // Matches TagCode, Name or Description (case-insensitive, partial).
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? CurrentLocationId { get; set; }
    public int? OwnerPartyId { get; set; }
    public int? CurrentContainerId { get; set; }
    public ItemStatus? Status { get; set; }
    public bool IncludeArchived { get; set; }
}

public class TrackedItemResponse
{
    public int Id { get; set; }
    public string TagCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReferenceSummary? Category { get; set; }
    public ReferenceSummary? CurrentLocation { get; set; }
    public ReferenceSummary? Owner { get; set; }
    public ReferenceSummary? CurrentContainer { get; set; }
    public ItemStatus Status { get; set; }
    public DateTime? LastEventAt { get; set; }
    public string? HsCode { get; set; }
    public string? OriginCountry { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? DeclaredValue { get; set; }
    public string? Currency { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public static TrackedItemResponse From(TrackedItem item) => new()
    {
        Id = item.Id,
        TagCode = item.TagCode,
        Name = item.Name,
        Description = item.Description,
        Category = item.Category == null
            ? null
            : new ReferenceSummary(item.Category.Id, item.Category.Code, item.Category.Name),
        CurrentLocation = item.CurrentLocation == null
            ? null
            : new ReferenceSummary(item.CurrentLocation.Id, item.CurrentLocation.Code, item.CurrentLocation.Name),
        Owner = item.OwnerParty == null
            ? null
            : new ReferenceSummary(item.OwnerParty.Id, item.OwnerParty.Code, item.OwnerParty.Name),
        CurrentContainer = item.CurrentContainer == null
            ? null
            : new ReferenceSummary(item.CurrentContainer.Id, item.CurrentContainer.Code, item.CurrentContainer.Type.ToString()),
        Status = item.Status,
        LastEventAt = item.LastEventAt,
        HsCode = item.HsCode,
        OriginCountry = item.OriginCountry,
        WeightKg = item.WeightKg,
        LengthCm = item.LengthCm,
        WidthCm = item.WidthCm,
        HeightCm = item.HeightCm,
        DeclaredValue = item.DeclaredValue,
        Currency = item.Currency,
        Attributes = item.Attributes,
        IsArchived = item.IsArchived,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        CreatedBy = item.CreatedBy
    };
}
