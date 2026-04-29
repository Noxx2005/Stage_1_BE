using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class ProfilesListResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("page")]
    public required int Page { get; set; }

    [JsonPropertyName("limit")]
    public required int Limit { get; set; }

    [JsonPropertyName("total")]
    public required int Total { get; set; }

    [JsonPropertyName("total_pages")]
    public required int TotalPages { get; set; }

    [JsonPropertyName("links")]
    public PaginationLinks? Links { get; set; }

    [JsonPropertyName("data")]
    public required List<ProfileDetailResponse> Data { get; set; }
}

public class PaginationLinks
{
    [JsonPropertyName("self")]
    public required string Self { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("prev")]
    public string? Prev { get; set; }
}
