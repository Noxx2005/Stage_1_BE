using HngStageOne.Api.DTOs.Responses;

namespace HngStageOne.Api.Middleware;

public class ApiVersionHeaderMiddleware
{
    private const string RequiredVersion = "1";
    private const string HeaderName = "X-API-Version";
    private readonly RequestDelegate _next;

    public ApiVersionHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isProfileApi = path.StartsWithSegments("/api/profiles")
            || path.StartsWithSegments("/api/v1/profiles");

        if (isProfileApi
            && !HttpMethods.IsOptions(context.Request.Method)
            && context.Request.Headers[HeaderName].FirstOrDefault() != RequiredVersion)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Status = "error",
                Message = "API version header required"
            });
            return;
        }

        await _next(context);
    }
}
