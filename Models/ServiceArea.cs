namespace MyFirstApi.Models;

// Which station serves an area and which sorting hub that station feeds. Used to
// find the first-mile (origin) and last-mile (destination) points of a route.
// An area is either one postal code, or a whole province (PostalCode null); a
// postal-code area takes precedence over its province.
public class ServiceArea : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // ISO 3166-1 alpha-2.
    public string Country { get; set; } = "TH";
    // Required for a province-wide area; informational on a postal-code area.
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    // Branch / drop point / hub / warehouse that picks up and delivers in this area.
    public int StationLocationId { get; set; }
    public Location StationLocation { get; set; } = null!;
    // Sorting hub the station sends to and receives from.
    public int HubLocationId { get; set; }
    public Location HubLocation { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
