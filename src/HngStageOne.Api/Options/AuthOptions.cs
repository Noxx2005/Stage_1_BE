namespace HngStageOne.Api.Options;

public class GitHubOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AuthorizationEndpoint { get; set; } = "";
    public string TokenEndpoint { get; set; } = "";
    public string UserEndpoint { get; set; } = "";
    public string EmailsEndpoint { get; set; } = "";
}

public class JwtOptions
{
    public string Issuer { get; set; } = "insighta-labs";
    public string Audience { get; set; } = "insighta-labs-clients";
    public string SigningKey { get; set; } = "replace-this-development-only-signing-key-minimum-32-chars";
    public int AccessTokenMinutes { get; set; } = 3;
    public int RefreshTokenMinutes { get; set; } = 5;
}

public class InsightaAuthOptions
{
    public string WebPortalUrl { get; set; } = "";
    public string BackendPublicUrl { get; set; } = "";
    public string CliCallbackMessage { get; set; } = "Login complete. You can return to the Insighta CLI.";
    public string AdminGitHubUsernames { get; set; } = "";
    public int OAuthSessionMinutes { get; set; } = 10;

    public HashSet<string> GetAdminUsernames()
    {
        return AdminGitHubUsernames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public class RateLimitOptions
{
    public int AuthPermitLimit { get; set; } = 10;
    public int ApiPermitLimit { get; set; } = 60;
    public int WindowMinutes { get; set; } = 1;
}

public class ExternalApiOptions
{
    public string GenderizeBaseUrl { get; set; } = "";
    public string AgifyBaseUrl { get; set; } = "";
    public string NationalizeBaseUrl { get; set; } = "";
}
