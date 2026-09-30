using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class VehicleRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TransportMode? Mode { get; set; }

    public int? CarrierId { get; set; }

    // Licence plate, aircraft registration or train set number.
    [StringLength(50)]
    public string? RegistrationNumber { get; set; }

    // Vessels only.
    [RegularExpression(@"^\d{7}$", ErrorMessage = "ImoNumber must be 7 digits.")]
    public string? ImoNumber { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? CapacityKg { get; set; }
}

public class VehicleQuery : MasterDataQuery
{
    public TransportMode? Mode { get; set; }
    public int? CarrierId { get; set; }
}

public class VehicleResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TransportMode Mode { get; set; }
    public ReferenceSummary? Carrier { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? ImoNumber { get; set; }
    public decimal? CapacityKg { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
