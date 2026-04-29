using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.ExternalApis;

public class AgifyResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}
