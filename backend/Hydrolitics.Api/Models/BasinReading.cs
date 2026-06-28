namespace Hydrolitics.Api.Models;

public class BasinReading
{
    public string Basin { get; set; } = "";
    public double Percent { get; set; }
    public double Litres { get; set; }
    public double DistanceCm { get; set; }
    public double DepthCm { get; set; }
    public DateTime Time { get; set; }
}