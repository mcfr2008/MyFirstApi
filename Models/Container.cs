using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ContainerType>))]
public enum ContainerType
{
    Pallet,
    Box,
    Container20GP,
    Container40GP,
    Container40HC,
    Reefer20,
    Reefer40,
    // Air cargo Unit Load Device.
    Uld,
    Other
}

// A handling unit that holds tracked items and/or other containers
// (item -> pallet -> container -> vessel). Scanning it records events for everything inside.
public class Container : IMasterData
{
    public int Id { get; set; }
    // For ISO shipping containers this is the ISO 6346 number, e.g. CSQU3054383.
    public string Code { get; set; } = string.Empty;
    public ContainerType Type { get; set; }
    public string? SealNumber { get; set; }
    public int? ParentContainerId { get; set; }
    public Container? ParentContainer { get; set; }
    public int? CurrentLocationId { get; set; }
    public Location? CurrentLocation { get; set; }
    public decimal? MaxPayloadKg { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static bool IsIsoContainer(ContainerType type) => type is
        ContainerType.Container20GP or ContainerType.Container40GP or ContainerType.Container40HC or
        ContainerType.Reefer20 or ContainerType.Reefer40;
}
