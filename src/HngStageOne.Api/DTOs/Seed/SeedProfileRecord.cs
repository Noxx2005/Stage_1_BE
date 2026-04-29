using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Seed;

public class SeedProfileRecord
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("gender")]
    public required string Gender { get; set; }

    [JsonPropertyName("gender_probability")]
    public required double GenderProbability { get; set; }

    [JsonPropertyName("age")]
    public required int Age { get; set; }

    [JsonPropertyName("age_group")]
    public required string AgeGroup { get; set; }

    [JsonPropertyName("country_id")]
    public required string CountryId { get; set; }

    [JsonPropertyName("country_name")]
    public required string CountryName { get; set; }

    [JsonPropertyName("country_probability")]
    public required double CountryProbability { get; set; }
}
