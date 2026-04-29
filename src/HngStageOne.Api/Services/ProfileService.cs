using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.Helpers;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Models;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

public class ProfileService : IProfileService
{
    private readonly IProfileRepository _repository;
    private readonly IGenderizeClient _genderizeClient;
    private readonly IAgifyClient _agifyClient;
    private readonly INationalizeClient _nationalizeClient;
    private readonly IProfileQueryValidator _queryValidator;
    private readonly INaturalLanguageProfileQueryParser _queryParser;

    public ProfileService(
        IProfileRepository repository,
        IGenderizeClient genderizeClient,
        IAgifyClient agifyClient,
        INationalizeClient nationalizeClient,
        IProfileQueryValidator queryValidator,
        INaturalLanguageProfileQueryParser queryParser)
    {
        _repository = repository;
        _genderizeClient = genderizeClient;
        _agifyClient = agifyClient;
        _nationalizeClient = nationalizeClient;
        _queryValidator = queryValidator;
        _queryParser = queryParser;
    }

    public async Task<SingleProfileResponse> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new MissingOrEmptyParameterException();
        }

        var name = request.Name.Trim();

        // Check if profile already exists
        var existingProfile = await _repository.GetByNameAsync(name, cancellationToken);
        if (existingProfile != null)
        {
            return new SingleProfileResponse
            {
                Status = "success",
                Message = "Profile already exists",
                Data = MapToProfileDetailResponse(existingProfile)
            };
        }

        // Call external APIs
        var genderResponse = await _genderizeClient.GetGenderAsync(name);
        var ageResponse = await _agifyClient.GetAgeAsync(name);
        var nationalityResponse = await _nationalizeClient.GetNationalityAsync(name);

        // Validate responses
        if (genderResponse == null || string.IsNullOrWhiteSpace(genderResponse.Gender) || genderResponse.Count == 0)
        {
            throw new InvalidUpstreamResponseException("Genderize");
        }

        if (ageResponse == null || ageResponse.Age == null)
        {
            throw new InvalidUpstreamResponseException("Agify");
        }

        if (nationalityResponse == null || nationalityResponse.Country == null || nationalityResponse.Country.Count == 0)
        {
            throw new InvalidUpstreamResponseException("Nationalize");
        }

        // Get the country with highest probability
        var topCountry = nationalityResponse.Country
            .OrderByDescending(c => c.Probability)
            .FirstOrDefault();

        if (topCountry == null || string.IsNullOrWhiteSpace(topCountry.CountryId))
        {
            throw new InvalidUpstreamResponseException("Nationalize");
        }

        // Classify age group
        string ageGroup = AgeGroupClassifier.Classify(ageResponse.Age.Value);

        // Create profile
        var profile = new Profile
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Gender = genderResponse.Gender.ToLower(),
            GenderProbability = Convert.ToDouble(genderResponse.Probability ?? 0),
            Age = ageResponse.Age.Value,
            AgeGroup = ageGroup,
            CountryId = topCountry.CountryId.ToUpper(),
            CountryName = CountryLookup.ResolveName(topCountry.CountryId),
            CountryProbability = Convert.ToDouble(topCountry.Probability ?? 0),
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(profile, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new SingleProfileResponse
        {
            Status = "success",
            Data = MapToProfileDetailResponse(profile)
        };
    }

    public async Task<SingleProfileResponse> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByIdAsync(id, cancellationToken);
        if (profile == null)
        {
            throw new ProfileNotFoundException();
        }

        return new SingleProfileResponse
        {
            Status = "success",
            Data = MapToProfileDetailResponse(profile)
        };
    }

    public async Task<ProfilesListResponse> GetProfilesAsync(ProfileQueryRequest request, CancellationToken cancellationToken = default)
    {
        var options = _queryValidator.Validate(request);
        var profiles = await _repository.QueryAsync(options, cancellationToken);
        return MapToProfilesListResponse(profiles);
    }

    public async Task<ProfilesListResponse> SearchProfilesAsync(ProfileSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Q))
        {
            throw new MissingOrEmptyParameterException();
        }

        var paging = _queryValidator.ValidateSearch(request.Page, request.Limit);
        var parsedOptions = _queryParser.Parse(request.Q);
        parsedOptions.Page = paging.Page;
        parsedOptions.Limit = paging.Limit;

        var profiles = await _repository.QueryAsync(parsedOptions, cancellationToken);
        return MapToProfilesListResponse(profiles);
    }

    public async Task<IReadOnlyList<ProfileDetailResponse>> ExportProfilesAsync(ProfileQueryRequest request, string? naturalLanguageQuery, CancellationToken cancellationToken = default)
    {
        ProfileQueryOptions options;
        if (!string.IsNullOrWhiteSpace(naturalLanguageQuery))
        {
            options = _queryParser.Parse(naturalLanguageQuery);
        }
        else
        {
            options = _queryValidator.Validate(request);
        }

        var profiles = await _repository.QueryAllAsync(options, cancellationToken);
        return profiles.Select(MapToProfileDetailResponse).ToList();
    }

    public async Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByIdAsync(id, cancellationToken);
        if (profile == null)
        {
            throw new ProfileNotFoundException();
        }

        await _repository.DeleteAsync(profile, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private static ProfileDetailResponse MapToProfileDetailResponse(Profile profile)
    {
        return new ProfileDetailResponse
        {
            Id = profile.Id,
            Name = profile.Name,
            Gender = profile.Gender,
            GenderProbability = profile.GenderProbability,
            Age = profile.Age,
            AgeGroup = profile.AgeGroup,
            CountryId = profile.CountryId,
            CountryName = profile.CountryName,
            CountryProbability = profile.CountryProbability,
            CreatedAt = profile.CreatedAt.ToUniversalTime().ToString("O")
        };
    }

    private static ProfilesListResponse MapToProfilesListResponse(PagedResult<Profile> profiles)
    {
        return new ProfilesListResponse
        {
            Status = "success",
            Page = profiles.Page,
            Limit = profiles.Limit,
            Total = profiles.Total,
            TotalPages = profiles.Limit <= 0 ? 0 : (int)Math.Ceiling(profiles.Total / (double)profiles.Limit),
            Data = profiles.Items.Select(MapToProfileDetailResponse).ToList()
        };
    }
}
