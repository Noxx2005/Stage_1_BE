using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Domain.Entities;

namespace HngStageOne.Api.Services.Interfaces;

public interface IAuthService
{
    Task<AuthStartResponse> StartGitHubLoginAsync(string clientType, HttpRequest request, CancellationToken cancellationToken = default);
    Task<(string ClientType, TokenResponse Tokens)> CompleteGitHubLoginAsync(string code, string state, HttpContext context, CancellationToken cancellationToken = default);
    Task<TokenResponse> CreateTestTokenAsync(string role, HttpContext context, CancellationToken cancellationToken = default);
    Task<TokenResponse?> ExchangeCliTokenAsync(string state, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshAsync(string refreshToken, HttpContext context, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
