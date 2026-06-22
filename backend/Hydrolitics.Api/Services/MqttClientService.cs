using System.Text.Json;
using Hydrolitics.Api.Models;
using Hydrolitics.Api.Configuration;
using MQTTnet;

namespace Hydrolitics.Api.Services;

public class MqttService : BackgroundService
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttOptions _options;

    public MqttService(ILogger<MqttService> logger, MqttOptions options )
    {
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var basinId = topic.Split('/')[1]; // "hydrolitics/basin1" -> "basin1"
            var raw = e.ApplicationMessage.ConvertPayloadToString();

            _logger.LogInformation("Message on {Topic}: {Payload}", topic, raw);

            try
            {
                var payload = JsonSerializer.Deserialize<SensorPayload>(raw);

                if (payload?.Error != null)
                {
                    _logger.LogWarning("Sensor error on {BasinId}: {Error}", basinId, payload.Error);
                    return;
                }

                _logger.LogInformation("Basin {BasinId}: {Percent}% full", basinId, payload?.Percent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message on {Topic}", topic);
            }

            await Task.CompletedTask;
        };

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port )
            .WithCredentials(_options.Username, _options.Password)
            .WithClientId("hydrolitics-backend")
            .Build();

        await client.ConnectAsync(options, stoppingToken);
        _logger.LogInformation("MQTT connected to {Host}", _options.Host);

        await client.SubscribeAsync(_options.Topic, cancellationToken: stoppingToken);
        _logger.LogInformation("MQTT subscribed to {Topic}", _options.Topic);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}