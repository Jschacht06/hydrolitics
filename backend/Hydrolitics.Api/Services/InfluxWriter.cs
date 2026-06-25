using Hydrolitics.Api.Configuration;
using Hydrolitics.Api.Models;
using InfluxDB3.Client;
using InfluxDB3.Client.Write;

namespace Hydrolitics.Api.Services;

public class InfluxWriter
{
    private readonly InfluxDBClient _client;

    public InfluxWriter(InfluxOptions options)
    {
        _client = new InfluxDBClient(options.Host, token: options.Token, database: options.Database);
    }

    public async Task SaveAsync(string basinId, SensorPayload reading, CancellationToken ct)
    {
        var point = PointData.Measurement("basin_level")
            .SetTag("basin", basinId)
            .SetField("percent", reading.Percent ?? 0)
            .SetField("litres", reading.Litres ?? 0)
            .SetField("distance_cm", reading.DistanceCm ?? 0)
            .SetField("depth_cm", reading.DepthCm ?? 0)
            .SetTimestamp(DateTime.UtcNow);

        await _client.WritePointAsync(point, cancellationToken: ct);
    }
}


// source https://github.com/InfluxCommunity/influxdb3-csharp


