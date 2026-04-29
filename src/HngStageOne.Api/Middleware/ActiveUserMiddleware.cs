using System.Security.Claims;
using HngStageOne.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Middleware;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (!Guid.TryParse(idValue, out var userId)
                || !await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { status = "error", message = "User is inactive" });
                return;
            }
        }

        await _next(context);
    }
}
