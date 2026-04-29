using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HngStageOne.Api.Constants;
using HngStageOne.Api.Data;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Options;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HngStageOne.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly GitHubOptions _gitHubOptions;
    private readonly InsightaAuthOptions _authOptions;

    public AuthService(
        AppDbContext dbContext,
        HttpClient httpClient,
        ITokenService tokenService,
        IOptions<GitHubOptions> gitHubOptions,
        IOptions<InsightaAuthOptions> authOptions)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _tokenService = tokenService;
        _gitHubOptions = gitHubOptions.Value;
        _authOptions = authOptions.Value;
    }

    public async Task<AuthStartResponse> StartGitHubLoginAsync(string clientType, HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_gitHubOptions.ClientId))
        {
            throw new InvalidOperationException("GitHub OAuth is not configured");
        }

        var normalizedClient = string.Equals(clientType, "cli", StringComparison.OrdinalIgnoreCase) ? "cli" : "web";
        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var redirectUri = BuildCallbackUrl(request);
        var expiresAt = DateTime.UtcNow.AddMinutes(_authOptions.OAuthSessionMinutes);

        var session = new OAuthSession
        {
            Id = Guid.CreateVersion7(),
            State = state,
            CodeVerifier = verifier,
            RedirectUri = redirectUri,
            ClientType = normalizedClient,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        await _dbContext.OAuthSessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var authorizationUrl = _gitHubOptions.AuthorizationEndpoint
            + $"?client_id={Uri.EscapeDataString(_gitHubOptions.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&scope=read:user%20user:email"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&code_challenge={Uri.EscapeDataString(challenge)}"
            + "&code_challenge_method=S256";

        return new AuthStartResponse(authorizationUrl, state, expiresAt);
    }

    public async Task<(string ClientType, TokenResponse Tokens)> CompleteGitHubLoginAsync(string code, string state, HttpContext context, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.OAuthSessions.FirstOrDefaultAsync(item => item.State == state, cancellationToken);
        if (session is null || session.ExpiresAt <= DateTime.UtcNow || session.ConsumedAt is not null)
        {
            throw new UnauthorizedAccessException("Invalid OAuth state");
        }

        var gitHubToken = await ExchangeCodeAsync(code, session, cancellationToken);
        var gitHubUser = await GetGitHubUserAsync(gitHubToken.AccessToken, cancellationToken);
        var email = gitHubUser.Email ?? await GetPrimaryEmailAsync(gitHubToken.AccessToken, cancellationToken);
        var user = await UpsertUserAsync(gitHubUser, email, cancellationToken);
        var tokens = await _tokenService.CreateTokenPairAsync(user, context, cancellationToken);

        session.ConsumedAt = DateTime.UtcNow;
        if (session.ClientType == "cli")
        {
            session.TokenResultJson = JsonSerializer.Serialize(tokens);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (session.ClientType, tokens);
    }

    public async Task<TokenResponse> CreateTestTokenAsync(string role, HttpContext context, CancellationToken cancellationToken = default)
    {
        var normalizedRole = string.Equals(role, AuthConstants.AnalystRole, StringComparison.OrdinalIgnoreCase)
            ? AuthConstants.AnalystRole
            : AuthConstants.AdminRole;
        var githubId = normalizedRole == AuthConstants.AdminRole ? -1001L : -1002L;
        var username = normalizedRole == AuthConstants.AdminRole ? "test-admin" : "test-analyst";
        var now = DateTime.UtcNow;

        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.GitHubId == githubId, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(),
                GitHubId = githubId,
                GitHubUsername = username,
                Email = $"{username}@insighta.test",
                AvatarUrl = null,
                Role = normalizedRole,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.GitHubUsername = username;
            user.Email = $"{username}@insighta.test";
            user.Role = normalizedRole;
            user.IsActive = true;
            user.UpdatedAt = now;
            user.LastLoginAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await _tokenService.CreateTokenPairAsync(user, context, cancellationToken);
    }

    public async Task<TokenResponse?> ExchangeCliTokenAsync(string state, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.OAuthSessions.FirstOrDefaultAsync(item => item.State == state, cancellationToken);
        if (session is null || session.ClientType != "cli" || session.ExpiresAt <= DateTime.UtcNow || string.IsNullOrWhiteSpace(session.TokenResultJson))
        {
            return null;
        }

        var tokens = JsonSerializer.Deserialize<TokenResponse>(session.TokenResultJson);
        session.TokenResultJson = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return tokens;
    }

    public Task<TokenResponse> RefreshAsync(string refreshToken, HttpContext context, CancellationToken cancellationToken = default)
    {
        return _tokenService.RefreshAsync(refreshToken, context, cancellationToken);
    }

    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return _tokenService.RevokeAsync(refreshToken, cancellationToken);
    }

    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    private async Task<GitHubTokenResponse> ExchangeCodeAsync(string code, OAuthSession session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _gitHubOptions.TokenEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _gitHubOptions.ClientId,
            ["client_secret"] = _gitHubOptions.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = session.RedirectUri,
            ["code_verifier"] = session.CodeVerifier
        });

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<GitHubTokenResponse>(cancellationToken);
        return token ?? throw new UnauthorizedAccessException("GitHub token exchange failed");
    }

    private async Task<GitHubUserResponse> GetGitHubUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _gitHubOptions.UserEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Insighta-Labs-Stage3");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken);
        return user ?? throw new UnauthorizedAccessException("GitHub user lookup failed");
    }

    private async Task<string?> GetPrimaryEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _gitHubOptions.EmailsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("Insighta-Labs-Stage3");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var emails = await response.Content.ReadFromJsonAsync<List<GitHubEmailResponse>>(cancellationToken);
        return emails?.FirstOrDefault(email => email.Primary && email.Verified)?.Email;
    }

    private async Task<User> UpsertUserAsync(GitHubUserResponse gitHubUser, string? email, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var adminUsernames = _authOptions.GetAdminUsernames();
        var role = adminUsernames.Contains(gitHubUser.Login) ? AuthConstants.AdminRole : AuthConstants.AnalystRole;

        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.GitHubId == gitHubUser.Id, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.CreateVersion7(),
                GitHubId = gitHubUser.Id,
                GitHubUsername = gitHubUser.Login,
                Email = email,
                AvatarUrl = gitHubUser.AvatarUrl,
                Role = role,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.GitHubUsername = gitHubUser.Login;
            user.Email = email;
            user.AvatarUrl = gitHubUser.AvatarUrl;
            user.Role = role;
            user.UpdatedAt = now;
            user.LastLoginAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private string BuildCallbackUrl(HttpRequest request)
    {
        var publicBase = _authOptions.BackendPublicUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(publicBase))
        {
            return $"{publicBase}/api/v1/auth/github/callback";
        }

        return $"{request.Scheme}://{request.Host}/api/v1/auth/github/callback";
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
