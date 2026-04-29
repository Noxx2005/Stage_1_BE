using System.Text.Json.Serialization;

namespace HngStageOne.Api.DTOs.Responses;

public class V1ProfilesListResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("data")]
    public required List<ProfileDetailResponse> Data { get; set; }

    [JsonPropertyName("pagination")]
    public required PaginationMetadata Pagination { get; set; }
}

public class PaginationMetadata
{
    [JsonPropertyName("page")]
    public required int Page { get; set; }

    [JsonPropertyName("limit")]
    public required int Limit { get; set; }

    [JsonPropertyName("total")]
    public required int Total { get; set; }

    [JsonPropertyName("total_pages")]
    public required int TotalPages { get; set; }

    [JsonPropertyName("has_next")]
    public required bool HasNext { get; set; }

    [JsonPropertyName("has_previous")]
    public required bool HasPrevious { get; set; }
}
