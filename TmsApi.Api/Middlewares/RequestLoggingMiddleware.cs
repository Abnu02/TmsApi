using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TmsApi.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
  
        string correlationId = Guid.NewGuid().ToString("N")[..8];

     
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        _logger.LogInformation(
            "Incoming request: {Method} {Path} [CorrelationId: {CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            correlationId
        );

 
        var stopwatch = Stopwatch.StartNew();

        try
        {

            await _next(context);
        }
        finally
        {
     
            stopwatch.Stop();

            _logger.LogInformation(
                "Completed request: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId
            );
        }
    }
}
