using System.Globalization;
using System.Text.RegularExpressions;

namespace HngStageOne.Api.Helpers;

public static partial class CountryLookup
{
    private static readonly IReadOnlyDictionary<string, string> CodeToNameMap = BuildCodeToNameMap();
    private static readonly IReadOnlyDictionary<string, string> AliasToCodeMap = BuildAliasToCodeMap();

    public static string ResolveName(string countryId)
    {
        if (CodeToNameMap.TryGetValue(countryId.Trim().ToUpperInvariant(), out var countryName))
        {
            return countryName;
        }

        return countryId.Trim().ToUpperInvariant();
    }

    public static bool TryResolveCode(string countryNameOrCode, out string countryId)
    {
        var normalized = Normalize(countryNameOrCode);
        if (AliasToCodeMap.TryGetValue(normalized, out countryId!))
        {
            return true;
        }

        countryId = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, string> BuildCodeToNameMap()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new RegionInfo(culture.Name))
            .GroupBy(region => region.TwoLetterISORegionName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key.ToUpperInvariant(),
                group => group.First().EnglishName,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> BuildAliasToCodeMap()
    {
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in CodeToNameMap)
        {
            aliasMap[Normalize(pair.Key)] = pair.Key;
            aliasMap[Normalize(pair.Value)] = pair.Key;
        }

        foreach (var alias in GetManualAliases())
        {
            aliasMap[Normalize(alias.Key)] = alias.Value;
        }

        return aliasMap;
    }

    private static IDictionary<string, string> GetManualAliases()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dr congo"] = "CD",
            ["drc"] = "CD",
            ["democratic republic of congo"] = "CD",
            ["democratic republic of the congo"] = "CD",
            ["ivory coast"] = "CI",
            ["cape verde"] = "CV",
            ["swaziland"] = "SZ",
            ["south korea"] = "KR",
            ["north korea"] = "KP",
            ["usa"] = "US",
            ["united states"] = "US",
            ["uk"] = "GB",
            ["great britain"] = "GB"
        };
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        return MultiWhitespaceRegex().Replace(trimmed, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
