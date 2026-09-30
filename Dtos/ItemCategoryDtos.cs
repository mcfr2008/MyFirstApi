using System.ComponentModel.DataAnnotations;

namespace MyFirstApi.Dtos;

public class ItemCategoryRequest : MasterDataRequest, IValidatableObject
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Attribute keys that items in this category must provide.
    public List<string>? RequiredAttributes { get; set; }

    public bool IsFragile { get; set; }
    public bool RequiresTemperatureControl { get; set; }

    // Dangerous goods: UnNumber and HazardClass are required when true.
    public bool IsDangerousGoods { get; set; }

    [RegularExpression(@"^(UN|un)\d{4}$", ErrorMessage = "UnNumber must look like UN1263.")]
    public string? UnNumber { get; set; }

    // e.g. "3" (flammable liquids), "2.1", "9".
    [RegularExpression(@"^[1-9](\.[1-6])?$", ErrorMessage = "HazardClass must be a class like 3 or 2.1.")]
    public string? HazardClass { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsDangerousGoods && (string.IsNullOrWhiteSpace(UnNumber) || string.IsNullOrWhiteSpace(HazardClass)))
        {
            yield return new ValidationResult(
                "Dangerous goods categories need both UnNumber and HazardClass.", [nameof(UnNumber), nameof(HazardClass)]);
        }

        if (RequiredAttributes == null) yield break;

        if (RequiredAttributes.Count > TrackedItemFields.MaxAttributes)
        {
            yield return new ValidationResult(
                $"At most {TrackedItemFields.MaxAttributes} required attributes are allowed.", [nameof(RequiredAttributes)]);
        }
        if (RequiredAttributes.Any(k => string.IsNullOrWhiteSpace(k) || k.Length > 100))
        {
            yield return new ValidationResult(
                "Required attribute keys must be 1-100 characters.", [nameof(RequiredAttributes)]);
        }
    }
}

public class ItemCategoryResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> RequiredAttributes { get; set; } = new();
    public bool IsFragile { get; set; }
    public bool RequiresTemperatureControl { get; set; }
    public bool IsDangerousGoods { get; set; }
    public string? UnNumber { get; set; }
    public string? HazardClass { get; set; }
    public bool IsActive { get; set; }
    // Non-archived items currently in this category.
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
