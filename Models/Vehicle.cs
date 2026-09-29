namespace MyFirstApi.Models;

// A truck, train, aircraft or vessel. Legs operated by outside carriers can
// skip this and just record VehicleName / VoyageNumber as text.
public class Vehicle : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public int? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    // Licence plate, aircraft registration (HS-TKA) or train set number.
    public string? RegistrationNumber { get; set; }
    // IMO ship identification number (vessels only), 7 digits.
    public string? ImoNumber { get; set; }
    public decimal? CapacityKg { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
