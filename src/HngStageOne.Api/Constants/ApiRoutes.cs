namespace HngStageOne.Api.Constants;

public static class ApiRoutes
{
    public const string ApiBase = "api";
    public const string ProfilesEndpoint = "profiles";

    public static class Profiles
    {
        public const string Base = $"{ApiBase}/{ProfilesEndpoint}";
        public const string GetById = $"{Base}/{{id}}";
        public const string Search = $"{Base}/search";
    }
}
