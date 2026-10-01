using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TransportMode>))]
public enum TransportMode
{
    Road,
    Rail,
    Air,
    Sea,
    // Last-mile delivery to the receiver.
    Courier
}

public static class TransportModes
{
    // Road and Courier are interchangeable (a courier van is a road vehicle).
    public static bool SameFamily(TransportMode a, TransportMode b) =>
        a == b || (IsRoad(a) && IsRoad(b));

    public static bool IsRoad(TransportMode mode) => mode is TransportMode.Road or TransportMode.Courier;
}
