namespace HngStageOne.Api.Options;

public static class ConfigurationValidationExtensions
{
    public static void ValidateStageThreeConfiguration(this WebApplicationBuilder builder)
    {
        var gitHub = builder.Configuration.GetSection("GitHub").Get<GitHubOptions>() ?? new GitHubOptions();
        var auth = builder.Configuration.GetSection("Auth").Get<InsightaAuthOptions>() ?? new InsightaAuthOptions();
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var externalApis = builder.Configuration.GetSection("ExternalApis").Get<ExternalApiOptions>() ?? new ExternalApiOptions();

        Require(gitHub.AuthorizationEndpoint, "GitHub__AuthorizationEndpoint");
        Require(gitHub.TokenEndpoint, "GitHub__TokenEndpoint");
        Require(gitHub.UserEndpoint, "GitHub__UserEndpoint");
        Require(gitHub.EmailsEndpoint, "GitHub__EmailsEndpoint");
        Require(auth.BackendPublicUrl, "Auth__BackendPublicUrl");
        Require(auth.WebPortalUrl, "Auth__WebPortalUrl");
        Require(jwt.SigningKey, "Jwt__SigningKey");
        Require(externalApis.GenderizeBaseUrl, "ExternalApis__GenderizeBaseUrl");
        Require(externalApis.AgifyBaseUrl, "ExternalApis__AgifyBaseUrl");
        Require(externalApis.NationalizeBaseUrl, "ExternalApis__NationalizeBaseUrl");
    }

    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required configuration value: {key}");
        }
    }
}
