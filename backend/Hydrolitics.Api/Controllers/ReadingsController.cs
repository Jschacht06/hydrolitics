using Hydrolitics.Api.Models;
using Hydrolitics.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hydrolitics.Api.Controllers;

[ApiController]
[Route("readings")]
public class ReadingsController : ControllerBase
{
    private readonly InfluxReader _reader;

    public ReadingsController(InfluxReader reader) => _reader = reader;

    [HttpGet("latest")]
    public async Task<ActionResult<IReadOnlyList<BasinReading>>> GetLatest(CancellationToken ct)
    {
        var readings = await _reader.GetLatestPerBasinAsync(ct);
        return Ok(readings);
    }
}