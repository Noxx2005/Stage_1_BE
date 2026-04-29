using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Services;

namespace HngStageOne.Api.Tests;

public class NaturalLanguageProfileQueryParserTests
{
    private readonly NaturalLanguageProfileQueryParser _parser = new();

    [Theory]
    [InlineData("young males", "male", null, null, 16, 24)]
    [InlineData("females above 30", "female", null, null, 30, null)]
    [InlineData("people from angola", null, null, "AO", null, null)]
    [InlineData("adult males from kenya", "male", "adult", "KE", null, null)]
    [InlineData("male and female teenagers above 17", null, "teenager", null, 17, null)]
    public void Parse_Should_Map_Supported_Queries(
        string query,
        string? expectedGender,
        string? expectedAgeGroup,
        string? expectedCountryId,
        int? expectedMinAge,
        int? expectedMaxAge)
    {
        var result = _parser.Parse(query);

        Assert.Equal(expectedGender, result.Gender);
        Assert.Equal(expectedAgeGroup, result.AgeGroup);
        Assert.Equal(expectedCountryId, result.CountryId);
        Assert.Equal(expectedMinAge, result.MinAge);
        Assert.Equal(expectedMaxAge, result.MaxAge);
    }

    [Fact]
    public void Parse_Should_Throw_When_Query_Cannot_Be_Interpreted()
    {
        Assert.Throws<UnableToInterpretQueryException>(() => _parser.Parse("show me everything"));
    }
}
