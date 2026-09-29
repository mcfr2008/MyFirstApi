using System.ComponentModel.DataAnnotations;

namespace MyFirstApi.Dtos;

public class ReasonCodeRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string NameTh { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string NameEn { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Event type codes this reason applies to (e.g. ["DELIVERY_FAILED"]); empty = any.
    public List<string>? EventTypeCodes { get; set; }

    public bool RequiresNote { get; set; }
    public int SortOrder { get; set; }
}

public class ReasonCodeQuery : MasterDataQuery
{
    // Only reasons usable with this event type (for a dropdown next to the event picker).
    public string? EventTypeCode { get; set; }
}

public class ReasonCodeResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> EventTypeCodes { get; set; } = new();
    public bool RequiresNote { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
