using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.Models;

namespace HngStageOne.Api.Services.Interfaces;

public interface IProfileQueryValidator
{
    ProfileQueryOptions Validate(ProfileQueryRequest request);
    ProfileQueryOptions ValidateSearch(string? page, string? limit);
}
