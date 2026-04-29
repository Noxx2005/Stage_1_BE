namespace HngStageOne.Api.Helpers;

public static class NameNormalizer
{
    public static string Normalize(string name)
    {
        return name.Trim().ToLower();
    }
}
