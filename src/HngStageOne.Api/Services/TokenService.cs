using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HngStageOne.Api.Data;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.DTOs.Auth;
using HngStageOne.Api.Options;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HngStageOne.Api.Services;

public class TokenService : ITokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public TokenService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<TokenResponse> CreateTokenPairAsync(User user, HttpContext context, CancellationToken cancellationToken = default)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshEntity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.RefreshTokenMinutes),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        await _dbContext.RefreshTokens.AddAsync(refreshEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildTokenResponse(user, refreshToken);
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken, HttpContext context, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(refreshToken);
        var existing = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (existing?.User is null || existing.ExpiresAt <= DateTime.UtcNow || existing.RevokedAt is not null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        existing.RevokedAt = DateTime.UtcNow;
        var next = await CreateTokenPairAsync(existing.User, context, cancellationToken);

        var nextHash = Hash(next.RefreshToken);
        var nextToken = await _dbContext.RefreshTokens.FirstAsync(token => token.TokenHash == nextHash, cancellationToken);
        existing.ReplacedByTokenId = nextToken.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return next;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = Hash(refreshToken);
        var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.RevokedAt ??= DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private TokenResponse BuildTokenResponse(User user, string refreshToken)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("github_id", user.GitHubId.ToString()),
            new("username", user.GitHubUsername),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            refreshToken,
            (int)TimeSpan.FromMinutes(_jwtOptions.AccessTokenMinutes).TotalSeconds,
            "Bearer",
            new AuthUserResponse(user.Id, user.GitHubUsername, user.Email, user.AvatarUrl, user.Role));
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
