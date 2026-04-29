using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfileDetailResponse
{
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }

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

    [JsonPropertyName("created_at")]
    public required string CreatedAt { get; set; }
}
