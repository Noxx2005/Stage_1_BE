using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ErrorResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
