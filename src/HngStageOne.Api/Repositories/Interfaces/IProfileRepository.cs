using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.Models;

namespace HngStageOne.Api.Repositories.Interfaces;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Profile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<PagedResult<Profile>> QueryAsync(ProfileQueryOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Profile>> QueryAllAsync(ProfileQueryOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetExistingNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
    Task AddAsync(Profile profile, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Profile> profiles, CancellationToken cancellationToken = default);
    Task DeleteAsync(Profile profile, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
