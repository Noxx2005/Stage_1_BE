using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Auth;

public record AuthStartResponse(
    [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("expires_at")] DateTime ExpiresAt);

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("user")] AuthUserResponse User);

public record AuthUserResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("github_username")] string GitHubUsername,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("role")] string Role);

public record RefreshTokenRequest(
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public record CliExchangeRequest(
    [property: JsonPropertyName("state")] string? State);

public record LogoutRequest(
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public record GitHubTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("scope")] string? Scope);

public record GitHubUserResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("email")] string? Email);

public record GitHubEmailResponse(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("primary")] bool Primary,
    [property: JsonPropertyName("verified")] bool Verified);
