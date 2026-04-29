using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.DTOs.Requests;

public class ProfileSearchRequest
{
    [FromQuery(Name = "q")]
    public string? Q { get; set; }

    [FromQuery(Name = "page")]
    public string? Page { get; set; }

    [FromQuery(Name = "limit")]
    public string? Limit { get; set; }
}
