using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class SingleProfileResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public required ProfileDetailResponse Data { get; set; }
}
