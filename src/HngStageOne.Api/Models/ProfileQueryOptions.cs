namespace HngStageOne.Api.Models;

public class ProfileQueryOptions
{
    public string? Gender { get; set; }
    public string? AgeGroup { get; set; }
    public string? CountryId { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public double? MinGenderProbability { get; set; }
    public double? MinCountryProbability { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string Order { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 10;
}
