using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Services;

namespace HngStageOne.Api.Tests;

public class ProfileQueryValidatorTests
{
    private readonly ProfileQueryValidator _validator = new();

    [Fact]
    public void Validate_Should_Parse_And_Normalize_Valid_Query()
    {
        var result = _validator.Validate(new ProfileQueryRequest
        {
            Gender = "Male",
            AgeGroup = "Adult",
            CountryId = "ng",
            MinAge = "25",
            MaxAge = "40",
            MinGenderProbability = "0.5",
            MinCountryProbability = "0.7",
            SortBy = "age",
            Order = "asc",
            Page = "2",
            Limit = "25"
        });

        Assert.Equal("male", result.Gender);
        Assert.Equal("adult", result.AgeGroup);
        Assert.Equal("NG", result.CountryId);
        Assert.Equal(25, result.MinAge);
        Assert.Equal(40, result.MaxAge);
        Assert.Equal(0.5, result.MinGenderProbability);
        Assert.Equal(0.7, result.MinCountryProbability);
        Assert.Equal("age", result.SortBy);
        Assert.Equal("asc", result.Order);
        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.Limit);
    }

    [Fact]
    public void Validate_Should_Throw_For_Invalid_Limit()
    {
        Assert.Throws<InvalidQueryParametersException>(() =>
            _validator.Validate(new ProfileQueryRequest { Limit = "51" }));
    }

    [Fact]
    public void Validate_Should_Throw_For_Empty_Filter_Value()
    {
        Assert.Throws<MissingOrEmptyParameterException>(() =>
            _validator.Validate(new ProfileQueryRequest { Gender = " " }));
    }
}
