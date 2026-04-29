using System.Text.Json;
using HngStageOne.Api.DTOs.Seed;

namespace HngStageOne.Api.Tests;

public class SeedProfileFileTests
{
    [Fact]
    public void Seed_File_Should_Contain_2026_Profiles()
    {
        var seedFilePath = FindSeedFilePath();
        var json = File.ReadAllText(seedFilePath);
        var seedFile = JsonSerializer.Deserialize<SeedProfileFile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(seedFile);
        Assert.NotNull(seedFile!.Profiles);
        Assert.Equal(2026, seedFile.Profiles.Count);
    }

    private static string FindSeedFilePath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var candidatePath = Path.Combine(currentDirectory.FullName, "seed_profiles.json");
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new FileNotFoundException("Could not locate seed_profiles.json from test output directory.");
    }
}
