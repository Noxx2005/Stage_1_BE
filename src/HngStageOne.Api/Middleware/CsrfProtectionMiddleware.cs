using HngStageOne.Api.Constants;

namespace HngStageOne.Api.Middleware;

public class CsrfProtectionMiddleware
{
    private static readonly HashSet<string> UnsafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    private readonly RequestDelegate _next;

    public CsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var usesCookieAuth = context.Request.Cookies.ContainsKey(AuthConstants.AccessTokenCookieName);
        var isUnsafe = UnsafeMethods.Contains(context.Request.Method);
        var isAuthEndpoint = context.Request.Path.StartsWithSegments("/api/v1/auth");

        if (usesCookieAuth && isUnsafe && !isAuthEndpoint)
        {
            var cookieToken = context.Request.Cookies[AuthConstants.CsrfCookieName];
            var headerToken = context.Request.Headers[AuthConstants.CsrfHeaderName].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken) || cookieToken != headerToken)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { status = "error", message = "Invalid CSRF token" });
                return;
            }
        }

        await _next(context);
    }
}
