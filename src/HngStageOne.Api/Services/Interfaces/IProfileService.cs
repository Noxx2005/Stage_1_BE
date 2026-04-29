using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;

namespace HngStageOne.Api.Services.Interfaces;

public interface IProfileService
{
    Task<SingleProfileResponse> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default);
    Task<SingleProfileResponse> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProfilesListResponse> GetProfilesAsync(ProfileQueryRequest request, CancellationToken cancellationToken = default);
    Task<ProfilesListResponse> SearchProfilesAsync(ProfileSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileDetailResponse>> ExportProfilesAsync(ProfileQueryRequest request, string? naturalLanguageQuery, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default);
}
