namespace HngStageOne.Api.Services.Interfaces;

public interface IProfileSeedService
{
    Task<int> SeedAsync(string filePath, CancellationToken cancellationToken = default);
}
