using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public class PartyRequest : MasterDataRequest
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public PartyType? Type { get; set; }

    [StringLength(20)]
    public string? TaxId { get; set; }

    [StringLength(255)]
    public string? ContactName { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? SubDistrict { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    // ISO 3166-1 alpha-2, defaults to TH.
    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string? Country { get; set; }
}

public class PartyQuery : MasterDataQuery
{
    public PartyType? Type { get; set; }
}

public class PartyResponse : IMasterDataResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PartyType Type { get; set; }
    public string? TaxId { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? SubDistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
