using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Helpers.Exceptions;
using System.Text.Json;

namespace HngStageOne.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        string message = "An unexpected error occurred";

        switch (exception)
        {
            case MissingOrEmptyParameterException missingParameterException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                message = missingParameterException.Message;
                break;

            case UnableToInterpretQueryException unableToInterpretQueryException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                message = unableToInterpretQueryException.Message;
                break;

            case InvalidQueryParametersException invalidQueryParametersException:
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                message = invalidQueryParametersException.Message;
                break;

            case ProfileNotFoundException:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                message = "Profile not found";
                break;

            case InvalidUpstreamResponseException upstreamEx:
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                message = $"{upstreamEx.ExternalApiName} returned an invalid response";
                break;

            case UnauthorizedAccessException unauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                message = unauthorizedAccessException.Message;
                break;

            case InvalidOperationException invalidOperationException:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                message = invalidOperationException.Message;
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                message = "An unexpected error occurred";
                break;
        }

        var response = new ErrorResponse
        {
            Status = "error",
            Message = message
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsJsonAsync(response, options);
    }
}
