using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<LocationType>))]
public enum LocationType
{
    Warehouse,
    Hub,
    Branch,
    DropPoint,
    CustomerAddress,
    Port,
    Airport,
    RailStation,
    ContainerYard,
    CustomsOffice,
    BorderCrossing,
    Other
}

public class Location : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LocationType Type { get; set; }
    public string? AddressLine { get; set; }
    public string? SubDistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "TH";
    // UN/LOCODE, e.g. THLCH (Laem Chabang port).
    public string? UnLocode { get; set; }
    // IATA airport code, e.g. BKK.
    public string? IataCode { get; set; }
    // IANA time zone for showing local times, e.g. Asia/Bangkok.
    public string TimeZone { get; set; } = "Asia/Bangkok";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
