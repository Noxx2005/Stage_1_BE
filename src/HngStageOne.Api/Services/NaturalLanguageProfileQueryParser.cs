using System.Text.RegularExpressions;
using HngStageOne.Api.Helpers;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Models;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

public partial class NaturalLanguageProfileQueryParser : INaturalLanguageProfileQueryParser
{
    public ProfileQueryOptions Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new MissingOrEmptyParameterException();
        }

        var normalized = NormalizeQuery(query);
        var options = new ProfileQueryOptions();
        var matchedSomething = false;

        var mentionsMale = GenderMaleRegex().IsMatch(normalized);
        var mentionsFemale = GenderFemaleRegex().IsMatch(normalized);

        if (mentionsMale ^ mentionsFemale)
        {
            options.Gender = mentionsMale ? "male" : "female";
            matchedSomething = true;
        }
        else if (mentionsMale || mentionsFemale)
        {
            matchedSomething = true;
        }

        if (AgeGroupTeenagerRegex().IsMatch(normalized))
        {
            options.AgeGroup = "teenager";
            matchedSomething = true;
        }
        else if (AgeGroupChildRegex().IsMatch(normalized))
        {
            options.AgeGroup = "child";
            matchedSomething = true;
        }
        else if (AgeGroupAdultRegex().IsMatch(normalized))
        {
            options.AgeGroup = "adult";
            matchedSomething = true;
        }
        else if (AgeGroupSeniorRegex().IsMatch(normalized))
        {
            options.AgeGroup = "senior";
            matchedSomething = true;
        }

        if (YoungRegex().IsMatch(normalized))
        {
            options.MinAge = 16;
            options.MaxAge = 24;
            matchedSomething = true;
        }

        var aboveMatch = AboveAgeRegex().Match(normalized);
        if (aboveMatch.Success)
        {
            options.MinAge = int.Parse(aboveMatch.Groups["age"].Value);
            matchedSomething = true;
        }

        var belowMatch = BelowAgeRegex().Match(normalized);
        if (belowMatch.Success)
        {
            options.MaxAge = int.Parse(belowMatch.Groups["age"].Value);
            matchedSomething = true;
        }

        var countryMatch = CountryRegex().Match(normalized);
        if (countryMatch.Success)
        {
            var countryText = countryMatch.Groups["country"].Value.Trim();
            if (!CountryLookup.TryResolveCode(countryText, out var countryId))
            {
                throw new UnableToInterpretQueryException();
            }

            options.CountryId = countryId;
            matchedSomething = true;
        }

        if (!matchedSomething)
        {
            throw new UnableToInterpretQueryException();
        }

        if (options.MinAge.HasValue && options.MaxAge.HasValue && options.MinAge > options.MaxAge)
        {
            throw new InvalidQueryParametersException();
        }

        return options;
    }

    private static string NormalizeQuery(string query)
    {
        return MultiWhitespaceRegex().Replace(query.Trim().ToLowerInvariant(), " ");
    }

    [GeneratedRegex(@"\b(male|males|man|men|boy|boys)\b")]
    private static partial Regex GenderMaleRegex();

    [GeneratedRegex(@"\b(female|females|woman|women|girl|girls)\b")]
    private static partial Regex GenderFemaleRegex();

    [GeneratedRegex(@"\b(teenager|teenagers|teen|teens)\b")]
    private static partial Regex AgeGroupTeenagerRegex();

    [GeneratedRegex(@"\b(child|children|kid|kids)\b")]
    private static partial Regex AgeGroupChildRegex();

    [GeneratedRegex(@"\b(adult|adults)\b")]
    private static partial Regex AgeGroupAdultRegex();

    [GeneratedRegex(@"\b(senior|seniors|elderly)\b")]
    private static partial Regex AgeGroupSeniorRegex();

    [GeneratedRegex(@"\byoung\b")]
    private static partial Regex YoungRegex();

    [GeneratedRegex(@"\b(?:above|over|older than|at least)\s+(?<age>\d{1,3})\b")]
    private static partial Regex AboveAgeRegex();

    [GeneratedRegex(@"\b(?:below|under|younger than|at most)\s+(?<age>\d{1,3})\b")]
    private static partial Regex BelowAgeRegex();

    [GeneratedRegex(@"\bfrom\s+(?<country>[a-z\s'.-]+?)(?=\b(?:above|over|older than|at least|below|under|younger than|at most)\b|$)")]
    private static partial Regex CountryRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();
}
