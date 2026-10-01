namespace MyFirstApi.Models;

// Greenhouse-gas intensity of moving freight by one mode, in grams CO2e per
// tonne-kilometre, well-to-wheel (fuel production + use), as used by ISO 14083 /
// the GLEC Framework. A row without CarrierId is the mode's default; a row with
// one is that carrier's own (usually reported) value and wins for its legs.
public class EmissionFactor : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public int? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    public decimal GramsCo2ePerTonneKm { get; set; }
    // Where the value comes from (GLEC default table, carrier report, ...).
    public string? Source { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
