using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Domain.Entities;

namespace HngStageOne.Api.Services.Interfaces;

public interface ITokenService
{
    Task<TokenResponse> CreateTokenPairAsync(User user, HttpContext context, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshAsync(string refreshToken, HttpContext context, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}
