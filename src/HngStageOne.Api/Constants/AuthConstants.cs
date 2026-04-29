namespace HngStageOne.Api.Constants;

public static class AuthConstants
{
    public const string AdminRole = "admin";
    public const string AnalystRole = "analyst";
    public const string AdminOnlyPolicy = "AdminOnly";
    public const string AnalystOrAdminPolicy = "AnalystOrAdmin";
    public const string AccessTokenCookieName = "insighta_access_token";
    public const string RefreshTokenCookieName = "insighta_refresh_token";
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";
}
