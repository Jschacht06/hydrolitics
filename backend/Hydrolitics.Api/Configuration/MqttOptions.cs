namespace Hydrolitics.Api.Configuration;

public class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Topic { get; set; } = "hydrolitics/#";
    public string ClientId { get; set; } = "hydrolitics-api";
}
