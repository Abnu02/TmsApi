using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/transcripts")]
[ApiVersion("2.0")]
public class TranscriptsController : ControllerBase
{
    /// <summary>
    /// Request a student transcript (stub — Exercise 5 upgrades this to a 202 Accepted + Location header pattern).
    /// The [EnableRateLimiting("transcripts")] attribute applies the named concurrency limiter ON TOP
    /// of the global tier-aware token bucket, giving two independent guards:
    ///   1. Token bucket: how many times per 10 s the caller can request.
    ///   2. Concurrency limiter: how many transcript jobs can run simultaneously (max 5; queue 20; 429 for the rest).
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    public IActionResult RequestTranscript([FromBody] object? _)
    {
        // Stub: Exercise 5 swaps this for enqueue + 202 + Location.
        return Ok(new { message = "Transcript request received." });
    }
}
