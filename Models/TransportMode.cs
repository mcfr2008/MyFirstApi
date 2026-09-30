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
