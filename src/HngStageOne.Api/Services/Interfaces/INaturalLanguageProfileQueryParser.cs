using HngStageOne.Api.Models;

namespace HngStageOne.Api.Services.Interfaces;

public interface INaturalLanguageProfileQueryParser
{
    ProfileQueryOptions Parse(string query);
}
