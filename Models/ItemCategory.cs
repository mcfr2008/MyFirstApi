namespace MyFirstApi.Models;

public class ItemCategory : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    // Attribute keys every TrackedItem in this category must fill in (e.g. "expiryDate").
    public List<string> RequiredAttributes { get; set; } = new();
    public bool IsFragile { get; set; }
    public bool RequiresTemperatureControl { get; set; }
    // Dangerous goods (IATA DGR / IMDG): UN number like "UN1263", hazard class like "3".
    public bool IsDangerousGoods { get; set; }
    public string? UnNumber { get; set; }
    public string? HazardClass { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
