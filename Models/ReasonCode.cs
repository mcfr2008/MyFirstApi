namespace MyFirstApi.Models;

// Why an exception event happened (recipient absent, water damage, missing
// documents, ...), so problems can be counted and analysed instead of living in free-text notes.
public class ReasonCode : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    // Event type codes this reason can be used with; empty = any event type.
    public List<string> EventTypeCodes { get; set; } = new();
    // e.g. OTHER: the event's note must explain.
    public bool RequiresNote { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
