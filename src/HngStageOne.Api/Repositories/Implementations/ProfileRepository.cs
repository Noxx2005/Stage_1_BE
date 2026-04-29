using HngStageOne.Api.Data;
using HngStageOne.Api.Domain.Entities;
using HngStageOne.Api.Models;
using HngStageOne.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Repositories.Implementations;

public class ProfileRepository : IProfileRepository
{
    private readonly AppDbContext _context;

    public ProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Profile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        return await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == normalizedName, cancellationToken);
    }

    public async Task<PagedResult<Profile>> QueryAsync(ProfileQueryOptions options, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(options);

        var total = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, options);

        var profiles = await query
            .Skip((options.Page - 1) * options.Limit)
            .Take(options.Limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<Profile>
        {
            Page = options.Page,
            Limit = options.Limit,
            Total = total,
            Items = profiles
        };
    }

    public async Task<IReadOnlyList<Profile>> QueryAllAsync(ProfileQueryOptions options, CancellationToken cancellationToken = default)
    {
        return await ApplySorting(ApplyFilters(options), options).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetExistingNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var normalizedNames = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedNames.Count == 0)
        {
            return [];
        }

        return await _context.Profiles
            .AsNoTracking()
            .Where(profile => normalizedNames.Contains(profile.Name))
            .Select(profile => profile.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        await _context.Profiles.AddAsync(profile, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Profile> profiles, CancellationToken cancellationToken = default)
    {
        await _context.Profiles.AddRangeAsync(profiles, cancellationToken);
    }

    public async Task DeleteAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        _context.Profiles.Remove(profile);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Profile> ApplySorting(IQueryable<Profile> query, ProfileQueryOptions options)
    {
        var descending = string.Equals(options.Order, "desc", StringComparison.OrdinalIgnoreCase);

        return (options.SortBy, descending) switch
        {
            ("age", true) => query.OrderByDescending(profile => profile.Age).ThenBy(profile => profile.Id),
            ("age", false) => query.OrderBy(profile => profile.Age).ThenBy(profile => profile.Id),
            ("gender_probability", true) => query.OrderByDescending(profile => profile.GenderProbability).ThenBy(profile => profile.Id),
            ("gender_probability", false) => query.OrderBy(profile => profile.GenderProbability).ThenBy(profile => profile.Id),
            ("created_at", false) => query.OrderBy(profile => profile.CreatedAt).ThenBy(profile => profile.Id),
            _ => query.OrderByDescending(profile => profile.CreatedAt).ThenBy(profile => profile.Id)
        };
    }

    private IQueryable<Profile> ApplyFilters(ProfileQueryOptions options)
    {
        var query = _context.Profiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(options.Gender))
        {
            query = query.Where(profile => profile.Gender == options.Gender);
        }

        if (!string.IsNullOrWhiteSpace(options.AgeGroup))
        {
            query = query.Where(profile => profile.AgeGroup == options.AgeGroup);
        }

        if (!string.IsNullOrWhiteSpace(options.CountryId))
        {
            query = query.Where(profile => profile.CountryId == options.CountryId);
        }

        if (options.MinAge.HasValue)
        {
            query = query.Where(profile => profile.Age >= options.MinAge.Value);
        }

        if (options.MaxAge.HasValue)
        {
            query = query.Where(profile => profile.Age <= options.MaxAge.Value);
        }

        if (options.MinGenderProbability.HasValue)
        {
            query = query.Where(profile => profile.GenderProbability >= options.MinGenderProbability.Value);
        }

        if (options.MinCountryProbability.HasValue)
        {
            query = query.Where(profile => profile.CountryProbability >= options.MinCountryProbability.Value);
        }

        return query;
    }
}
