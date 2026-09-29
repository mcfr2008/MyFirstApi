using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class EventTypeRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string NameTh { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string NameEn { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Omit/null for informational events that don't change the item's status.
    public ItemStatus? ResultingStatus { get; set; }

    public bool IsTerminal { get; set; }
    public int SortOrder { get; set; }
}

public class EventTypeResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ItemStatus? ResultingStatus { get; set; }
    public bool IsTerminal { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
