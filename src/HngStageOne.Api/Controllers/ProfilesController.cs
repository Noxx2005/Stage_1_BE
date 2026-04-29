using HngStageOne.Api.Constants;
using System.Text;
using HngStageOne.Api.DTOs.Requests;
using HngStageOne.Api.DTOs.Responses;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HngStageOne.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthConstants.AnalystOrAdminPolicy)]
[Route(ApiRoutes.Profiles.Base)]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfilesController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost]
    [Authorize(Policy = AuthConstants.AdminOnlyPolicy)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.CreateProfileAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { id = result.Data.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken cancellationToken)
    {
        var result = await _profileService.GetProfileByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllProfiles([FromQuery] ProfileQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.GetProfilesAsync(request, cancellationToken);
        AddPaginationLinks(result);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchProfiles([FromQuery] ProfileSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _profileService.SearchProfilesAsync(request, cancellationToken);
        AddPaginationLinks(result);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportProfiles([FromQuery] ProfileQueryRequest request, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { status = "error", message = "format=csv is required" });
        }

        var profiles = await _profileService.ExportProfilesAsync(request, null, cancellationToken);
        var csv = BuildCsv(profiles);
        var fileName = $"profiles_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthConstants.AdminOnlyPolicy)]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken cancellationToken)
    {
        await _profileService.DeleteProfileAsync(id, cancellationToken);
        return NoContent();
    }

    private void AddPaginationLinks(ProfilesListResponse response)
    {
        response.Links = BuildLinks(Request, response.Page, response.Limit, response.TotalPages);
    }

    private static PaginationLinks BuildLinks(HttpRequest request, int page, int limit, int totalPages)
    {
        string LinkFor(int targetPage)
        {
            var values = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.QueryString.Value);
            values["page"] = targetPage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            values["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(request.Path.Value ?? "/api/profiles", values.ToDictionary(item => item.Key, item => (string?)item.Value.ToString()));
        }

        return new PaginationLinks
        {
            Self = LinkFor(page),
            Next = page < totalPages ? LinkFor(page + 1) : null,
            Prev = page > 1 ? LinkFor(page - 1) : null
        };
    }

    private static string BuildCsv(IEnumerable<ProfileDetailResponse> profiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,name,gender,gender_probability,age,age_group,country_id,country_name,country_probability,created_at");
        foreach (var profile in profiles)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Escape(profile.Id.ToString()),
                Escape(profile.Name),
                Escape(profile.Gender),
                Escape(profile.GenderProbability.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.Age.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.AgeGroup),
                Escape(profile.CountryId),
                Escape(profile.CountryName),
                Escape(profile.CountryProbability.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Escape(profile.CreatedAt)
            }));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var text = value ?? "";
        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
