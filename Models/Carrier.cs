namespace MyFirstApi.Models;

// A transport provider: trucking company, railway, airline, shipping line, courier.
public class Carrier : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // TransportMode names; stored as text[] so it can be filtered in SQL.
    public List<string> Modes { get; set; } = new();
    // Standard Carrier Alpha Code (shipping lines / truckers), e.g. MAEU.
    public string? ScacCode { get; set; }
    // IATA airline designator, e.g. TG.
    public string? IataCode { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    // Carrier's public tracking page; "{number}" is replaced by the document number.
    public string? TrackingUrlTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
