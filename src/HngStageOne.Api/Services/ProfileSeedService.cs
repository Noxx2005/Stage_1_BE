using System.Text.Json;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.DTOs.Seed;
using HngStageOne.Api.Helpers.Exceptions;
using HngStageOne.Api.Repositories.Interfaces;
using HngStageOne.Api.Services.Interfaces;

namespace HngStageOne.Api.Services;

public class ProfileSeedService : IProfileSeedService
{
    private readonly IProfileRepository _repository;

    public ProfileSeedService(IProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> SeedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new MissingOrEmptyParameterException();
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Seed file not found", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var seedFile = await JsonSerializer.DeserializeAsync<SeedProfileFile>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (seedFile?.Profiles is null || seedFile.Profiles.Count == 0)
        {
            throw new InvalidQueryParametersException();
        }

        var incomingNames = seedFile.Profiles
            .Select(profile => profile.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var existingNames = await _repository.GetExistingNamesAsync(incomingNames, cancellationToken);
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var profilesToInsert = seedFile.Profiles
            .GroupBy(record => record.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(record => !existingSet.Contains(record.Name))
            .Select(MapToProfile)
            .ToList();

        if (profilesToInsert.Count == 0)
        {
            return 0;
        }

        await _repository.AddRangeAsync(profilesToInsert, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return profilesToInsert.Count;
    }

    private static Profile MapToProfile(SeedProfileRecord record)
    {
        return new Profile
        {
            Id = Guid.CreateVersion7(),
            Name = record.Name.Trim(),
            Gender = record.Gender.Trim().ToLowerInvariant(),
            GenderProbability = record.GenderProbability,
            Age = record.Age,
            AgeGroup = record.AgeGroup.Trim().ToLowerInvariant(),
            CountryId = record.CountryId.Trim().ToUpperInvariant(),
            CountryName = record.CountryName.Trim(),
            CountryProbability = record.CountryProbability,
            CreatedAt = DateTime.UtcNow
        };
    }
}
