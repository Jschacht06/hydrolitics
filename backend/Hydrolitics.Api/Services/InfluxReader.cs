using System.Globalization;
using Hydrolitics.Api.Configuration;
using Hydrolitics.Api.Models;
using InfluxDB3.Client;

namespace Hydrolitics.Api.Services;

public class InfluxReader
{
    private readonly InfluxDBClient _client;

    public InfluxReader(InfluxOptions options)
    {
        _client = new InfluxDBClient(options.Host, token: options.Token, database: options.Database);
    }

    public async Task<IReadOnlyList<BasinReading>> GetLatestPerBasinAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT basin, percent, litres, distance_cm, depth_cm, time
            FROM (
                SELECT basin, percent, litres, distance_cm, depth_cm, time,
                       ROW_NUMBER() OVER (PARTITION BY basin ORDER BY time DESC) AS rn
                FROM basin_level
            ) AS ranked
            WHERE rn = 1
            ORDER BY basin
            """;

        var results = new List<BasinReading>();

        await foreach (var row in _client.Query(query: sql).WithCancellation(ct))
        {
            results.Add(new BasinReading
            {
                Basin      = row[0]?.ToString() ?? "unknown",
                Percent    = ToDouble(row[1]),
                Litres     = ToDouble(row[2]),
                DistanceCm = ToDouble(row[3]),
                DepthCm    = ToDouble(row[4]),
                Time       = ToUtc(row[5]),
            });
        }

        return results;
    }

    private static double ToDouble(object? value) =>
        value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static DateTime ToUtc(object? value) => value switch
    {
        DateTime dt        => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        DateTimeOffset dto => dto.UtcDateTime,
        long ns            => DateTimeOffset.FromUnixTimeMilliseconds(ns / 1_000_000).UtcDateTime,
        _                  => DateTime.UtcNow,
    };
}