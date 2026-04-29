using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.ExternalApis;

public class GenderizeResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("probability")]
    public decimal? Probability { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
