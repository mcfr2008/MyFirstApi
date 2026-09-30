using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class ContainerRequest : MasterDataRequest
{
    [Required]
    public ContainerType? Type { get; set; }

    [StringLength(50)]
    public string? SealNumber { get; set; }

    // Put this container inside another one (pallet -> container).
    public int? ParentContainerId { get; set; }

    public int? CurrentLocationId { get; set; }

    [Range(typeof(decimal), "0", "1000000")]
    public decimal? MaxPayloadKg { get; set; }
}

public class ContainerQuery : MasterDataQuery
{
    public ContainerType? Type { get; set; }
    public int? ParentContainerId { get; set; }
    public int? CurrentLocationId { get; set; }
}

public class ContainerResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public ContainerType Type { get; set; }
    public string? SealNumber { get; set; }
    public ReferenceSummary? ParentContainer { get; set; }
    public ReferenceSummary? CurrentLocation { get; set; }
    public decimal? MaxPayloadKg { get; set; }
    // Directly inside this container (not counting nested containers' contents).
    public int ItemCount { get; set; }
    public int ChildContainerCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ContainerLoadRequest : IValidatableObject
{
    public List<int>? TrackedItemIds { get; set; }
    public List<string>? TagCodes { get; set; }
    public List<int>? ChildContainerIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var count = (TrackedItemIds?.Count ?? 0) + (TagCodes?.Count ?? 0) + (ChildContainerIds?.Count ?? 0);
        if (count == 0)
        {
            yield return new ValidationResult("Provide items (TrackedItemIds/TagCodes) or ChildContainerIds.");
        }
        if (count > 500)
        {
            yield return new ValidationResult("At most 500 entries per request.");
        }
    }
}

// Leave everything empty to unload the whole container.
public class ContainerUnloadRequest
{
    public List<int>? TrackedItemIds { get; set; }
    public List<string>? TagCodes { get; set; }
    public List<int>? ChildContainerIds { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class ContainerContentsResponse
{
    public ContainerResponse Container { get; set; } = null!;
    public List<TrackedItemResponse> Items { get; set; } = new();
    public List<ContainerContentsResponse> ChildContainers { get; set; } = new();
    // Items in this container and every nested container.
    public int TotalItemCount { get; set; }
}
