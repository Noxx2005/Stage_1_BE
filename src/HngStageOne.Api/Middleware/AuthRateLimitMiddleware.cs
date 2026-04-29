using System.Collections.Concurrent;
using HngStageOne.Api.Options;
using Microsoft.Extensions.Options;

namespace HngStageOne.Api.Middleware;

public class AuthRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, Counter> Counters = new();
    private readonly RequestDelegate _next;
    private readonly RateLimitOptions _options;

    public AuthRateLimitMiddleware(RequestDelegate next, IOptions<RateLimitOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/auth")
            && !context.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            await _next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes));
        var key = $"{context.Connection.RemoteIpAddress}|{context.Request.Path}";
        var counter = Counters.AddOrUpdate(
            key,
            _ => new Counter(now.Add(window), 1),
            (_, current) => current.ExpiresAt <= now
                ? new Counter(now.Add(window), 1)
                : current with { Count = current.Count + 1 });

        if (counter.Count == _options.AuthPermitLimit + 1)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = window.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await context.Response.WriteAsJsonAsync(new { status = "error", message = "Too many requests" });
            return;
        }

        await _next(context);
    }

    private sealed record Counter(DateTimeOffset ExpiresAt, int Count);
}
