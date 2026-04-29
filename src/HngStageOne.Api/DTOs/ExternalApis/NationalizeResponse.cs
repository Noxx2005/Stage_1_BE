using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.ExternalApis;

public class NationalizeResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("country")]
    public List<CountryData>? Country { get; set; }
}

public class CountryData
{
    [JsonPropertyName("country_id")]
    public string? CountryId { get; set; }

    [JsonPropertyName("probability")]
    public decimal? Probability { get; set; }
}
