using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.DTOs.Requests;

public class ProfileQueryRequest
{
    [FromQuery(Name = "gender")]
    public string? Gender { get; set; }

    [FromQuery(Name = "age_group")]
    public string? AgeGroup { get; set; }

    [FromQuery(Name = "country_id")]
    public string? CountryId { get; set; }

    [FromQuery(Name = "min_age")]
    public string? MinAge { get; set; }

    [FromQuery(Name = "max_age")]
    public string? MaxAge { get; set; }

    [FromQuery(Name = "min_gender_probability")]
    public string? MinGenderProbability { get; set; }

    [FromQuery(Name = "min_country_probability")]
    public string? MinCountryProbability { get; set; }

    [FromQuery(Name = "sort_by")]
    public string? SortBy { get; set; }

    [FromQuery(Name = "order")]
    public string? Order { get; set; }

    [FromQuery(Name = "page")]
    public string? Page { get; set; }

    [FromQuery(Name = "limit")]
    public string? Limit { get; set; }
}
