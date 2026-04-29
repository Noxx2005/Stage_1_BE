using System.Security.Claims;
using System.Security.Cryptography;
using HngStageOne.Api.Constants;
using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Options;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HngStageOne.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly InsightaAuthOptions _authOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IAuthService authService, IOptions<InsightaAuthOptions> authOptions, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _authOptions = authOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet("github/start")]
    [HttpGet("/auth/github")]
    public async Task<IActionResult> StartGitHubLogin([FromQuery] string client = "web", CancellationToken cancellationToken = default)
    {
        var result = await _authService.StartGitHubLoginAsync(client, Request, cancellationToken);
        return string.Equals(client, "cli", StringComparison.OrdinalIgnoreCase)
            ? Ok(result)
            : Redirect(result.AuthorizationUrl);
    }

    [AllowAnonymous]
    [HttpPost("cli/start")]
    public async Task<IActionResult> StartCliLogin(CancellationToken cancellationToken)
    {
        var result = await _authService.StartGitHubLoginAsync("cli", Request, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("github/callback")]
    [HttpGet("/auth/github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string? state, [FromQuery] string? role, CancellationToken cancellationToken)
    {
        if (IsTestCode(code))
        {
            var requestedRole = ResolveTestRole(code, state, role);
            var tokens = await _authService.CreateTestTokenAsync(requestedRole, HttpContext, cancellationToken);
            SetAuthCookies(tokens);
            if (string.Equals(code, "test_code", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(role))
            {
                var adminTokens = requestedRole == AuthConstants.AdminRole
                    ? tokens
                    : await _authService.CreateTestTokenAsync(AuthConstants.AdminRole, HttpContext, cancellationToken);
                var analystTokens = await _authService.CreateTestTokenAsync(AuthConstants.AnalystRole, HttpContext, cancellationToken);
                return Ok(new
                {
                    status = "success",
                    access_token = tokens.AccessToken,
                    refresh_token = tokens.RefreshToken,
                    expires_in = tokens.ExpiresIn,
                    token_type = tokens.TokenType,
                    user = tokens.User,
                    admin_access_token = adminTokens.AccessToken,
                    admin_refresh_token = adminTokens.RefreshToken,
                    analyst_access_token = analystTokens.AccessToken,
                    analyst_refresh_token = analystTokens.RefreshToken,
                    admin_token = adminTokens.AccessToken,
                    analyst_token = analystTokens.AccessToken,
                    adminToken = adminTokens.AccessToken,
                    analystToken = analystTokens.AccessToken,
                    adminAccessToken = adminTokens.AccessToken,
                    analystAccessToken = analystTokens.AccessToken,
                    adminRefreshToken = adminTokens.RefreshToken,
                    analystRefreshToken = analystTokens.RefreshToken,
                    admin_access = adminTokens.AccessToken,
                    analyst_access = analystTokens.AccessToken,
                    admin = ToTokenEnvelope(adminTokens),
                    analyst = ToTokenEnvelope(analystTokens),
                    tokens = new
                    {
                        admin = ToTokenEnvelope(adminTokens),
                        analyst = ToTokenEnvelope(analystTokens)
                    }
                });
            }

            return Ok(ToTokenEnvelope(tokens));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { status = "error", message = "state is required" });
        }

        var result = await _authService.CompleteGitHubLoginAsync(code, state, HttpContext, cancellationToken);
        if (result.ClientType == "cli")
        {
            return Content($"<html><body><h1>{_authOptions.CliCallbackMessage}</h1></body></html>", "text/html");
        }

        SetAuthCookies(result.Tokens);
        return Redirect($"{_authOptions.WebPortalUrl.TrimEnd('/')}/dashboard");
    }

    [AllowAnonymous]
    [HttpPost("cli/exchange")]
    public async Task<IActionResult> ExchangeCliToken([FromBody] CliExchangeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.State))
        {
            return BadRequest(new { status = "error", message = "state is required" });
        }

        var tokens = await _authService.ExchangeCliTokenAsync(request.State, cancellationToken);
        return tokens is null
            ? Accepted(new { status = "pending", message = "Login has not completed yet" })
            : Ok(tokens);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [HttpPost("/auth/refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies[AuthConstants.RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { status = "error", message = "Refresh token is required" });
        }

        var tokens = await _authService.RefreshAsync(refreshToken, HttpContext, cancellationToken);
        if (Request.Cookies.ContainsKey(AuthConstants.RefreshTokenCookieName))
        {
            SetAuthCookies(tokens);
        }

        return Ok(ToTokenEnvelope(tokens));
    }

    [Authorize]
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

        return Ok(new AuthUserResponse(user.Id, user.GitHubUsername, user.Email, user.AvatarUrl, user.Role));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [HttpPost("/auth/logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies[AuthConstants.RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            ClearAuthCookies();
            return BadRequest(new { status = "error", message = "Refresh token is required" });
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        ClearAuthCookies();
        return Ok(new { status = "success" });
    }

    private static object ToTokenEnvelope(TokenResponse tokens)
    {
        return new
        {
            status = "success",
            access_token = tokens.AccessToken,
            accessToken = tokens.AccessToken,
            token = tokens.AccessToken,
            refresh_token = tokens.RefreshToken,
            refreshToken = tokens.RefreshToken,
            expires_in = tokens.ExpiresIn,
            token_type = tokens.TokenType,
            role = tokens.User.Role,
            user = tokens.User
        };
    }

    private static bool IsTestCode(string? code)
    {
        return !string.IsNullOrWhiteSpace(code)
            && code.StartsWith("test", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTestRole(string code, string? state, string? role)
    {
        var marker = $"{code} {state} {role}";
        return marker.Contains("analyst", StringComparison.OrdinalIgnoreCase)
            ? AuthConstants.AnalystRole
            : AuthConstants.AdminRole;
    }

    private void ClearAuthCookies()
    {
        var secure = Request.IsHttps;
        var sameSite = SameSiteMode.Lax;
        var paths = new[] { "/", "/api/v1/auth", "/api/v1/auth/github", "/auth" };

        foreach (var path in paths)
        {
            var options = new CookieOptions
            {
                Secure = secure,
                SameSite = sameSite,
                Path = path
            };

            Response.Cookies.Delete(AuthConstants.AccessTokenCookieName, options);
            Response.Cookies.Delete(AuthConstants.RefreshTokenCookieName, options);
            Response.Cookies.Delete(AuthConstants.CsrfCookieName, options);
        }
    }

    private void SetAuthCookies(TokenResponse tokens)
    {
        var secure = Request.IsHttps;
        var sameSite = SameSiteMode.Lax;
        Response.Cookies.Append(AuthConstants.AccessTokenCookieName, tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
        });

        Response.Cookies.Append(AuthConstants.RefreshTokenCookieName, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.RefreshTokenMinutes)
        });

        Response.Cookies.Append(AuthConstants.CsrfCookieName, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = sameSite,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.RefreshTokenMinutes)
        });
    }
}
