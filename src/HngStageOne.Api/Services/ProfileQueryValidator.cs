using System.Globalization;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Models;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

public class ProfileQueryValidator : IProfileQueryValidator
{
    private static readonly HashSet<string> ValidGenders = ["male", "female"];
    private static readonly HashSet<string> ValidAgeGroups = ["child", "teenager", "adult", "senior"];
    private static readonly HashSet<string> ValidSortFields = ["age", "created_at", "gender_probability"];
    private static readonly HashSet<string> ValidOrders = ["asc", "desc"];

    public ProfileQueryOptions Validate(ProfileQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = new ProfileQueryOptions
        {
            Gender = NormalizeEnum(request.Gender, ValidGenders),
            AgeGroup = NormalizeEnum(request.AgeGroup, ValidAgeGroups),
            CountryId = NormalizeCountryId(request.CountryId),
            MinAge = ParseInt(request.MinAge),
            MaxAge = ParseInt(request.MaxAge),
            MinGenderProbability = ParseProbability(request.MinGenderProbability),
            MinCountryProbability = ParseProbability(request.MinCountryProbability),
            SortBy = NormalizeOptionalEnum(request.SortBy, ValidSortFields, "created_at"),
            Order = NormalizeOptionalEnum(request.Order, ValidOrders, "desc"),
            Page = ParsePagingValue(request.Page, 1),
            Limit = ParseLimit(request.Limit, 10)
        };

        EnsureValidRange(options.MinAge, options.MaxAge);
        return options;
    }

    public ProfileQueryOptions ValidateSearch(string? page, string? limit)
    {
        return new ProfileQueryOptions
        {
            Page = ParsePagingValue(page, 1),
            Limit = ParseLimit(limit, 10)
        };
    }

    private static string? NormalizeEnum(string? rawValue, HashSet<string> allowedValues)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new MissingOrEmptyParameterException();
        }

        var normalized = rawValue.Trim().ToLowerInvariant();
        if (!allowedValues.Contains(normalized))
        {
            throw new InvalidQueryParametersException();
        }

        return normalized;
    }

    private static string NormalizeOptionalEnum(string? rawValue, HashSet<string> allowedValues, string defaultValue)
    {
        if (rawValue is null)
        {
            return defaultValue;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new MissingOrEmptyParameterException();
        }

        var normalized = rawValue.Trim().ToLowerInvariant();
        if (!allowedValues.Contains(normalized))
        {
            throw new InvalidQueryParametersException();
        }

        return normalized;
    }

    private static string? NormalizeCountryId(string? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new MissingOrEmptyParameterException();
        }

        var normalized = rawValue.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || !normalized.All(char.IsLetter))
        {
            throw new InvalidQueryParametersException();
        }

        return normalized;
    }

    private static int? ParseInt(string? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new MissingOrEmptyParameterException();
        }

        if (!int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new InvalidQueryParametersException();
        }

        if (parsedValue < 0)
        {
            throw new InvalidQueryParametersException();
        }

        return parsedValue;
    }

    private static double? ParseProbability(string? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new MissingOrEmptyParameterException();
        }

        if (!double.TryParse(rawValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new InvalidQueryParametersException();
        }

        if (parsedValue is < 0 or > 1)
        {
            throw new InvalidQueryParametersException();
        }

        return parsedValue;
    }

    private static int ParsePagingValue(string? rawValue, int defaultValue)
    {
        var parsedValue = ParseInt(rawValue);
        if (parsedValue is null)
        {
            return defaultValue;
        }

        if (parsedValue < 1)
        {
            throw new InvalidQueryParametersException();
        }

        return parsedValue.Value;
    }

    private static int ParseLimit(string? rawValue, int defaultValue)
    {
        var parsedValue = ParseInt(rawValue);
        if (parsedValue is null)
        {
            return defaultValue;
        }

        if (parsedValue is < 1 or > 50)
        {
            throw new InvalidQueryParametersException();
        }

        return parsedValue.Value;
    }

    private static void EnsureValidRange(int? minAge, int? maxAge)
    {
        if (minAge.HasValue && maxAge.HasValue && minAge > maxAge)
        {
            throw new InvalidQueryParametersException();
        }
    }
}
