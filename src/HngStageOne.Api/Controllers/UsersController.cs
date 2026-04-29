using System.Security.Claims;
using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(idValue, out var userId))
        {
            return Unauthorized(new { status = "error", message = "Invalid user" });
        }

        var user = await _authService.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { status = "error", message = "Invalid user" });
        }

        if (!user.IsActive)
        {
            return Forbid();
        }

        var response = new AuthUserResponse(user.Id, user.GitHubUsername, user.Email, user.AvatarUrl, user.Role);
        return Ok(new
        {
            status = "success",
            id = response.Id,
            github_id = user.GitHubId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            username = user.GitHubUsername,
            github_username = response.GitHubUsername,
            email = response.Email,
            avatar_url = response.AvatarUrl,
            role = response.Role,
            is_active = user.IsActive,
            data = new
            {
                id = response.Id,
                github_id = user.GitHubId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                username = user.GitHubUsername,
                github_username = response.GitHubUsername,
                email = response.Email,
                avatar_url = response.AvatarUrl,
                role = response.Role,
                is_active = user.IsActive
            }
        });
    }
}
