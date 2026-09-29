namespace MyFirstApi.Models;

// Catalog of tracking events (picked up, arrived at hub, delivered, ...).
// ResultingStatus is the ItemStatus an item moves to when this event is recorded;
// null means the event is informational and leaves the status unchanged.
public class EventType : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameTh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ItemStatus? ResultingStatus { get; set; }
    // No further events are expected after a terminal one (e.g. Delivered).
    public bool IsTerminal { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
