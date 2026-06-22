using System.Text.Json.Serialization;

namespace Hydrolitics.Api.Models;

public class SensorPayload
{
    [JsonPropertyName("distance_cm")]
    public double? DistanceCm { get; set; }

    [JsonPropertyName("depth_cm")]
    public double? DepthCm { get; set; }

    [JsonPropertyName("percent")]
    public double? Percent { get; set; }

    [JsonPropertyName("litres")]
    public double? Litres { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}