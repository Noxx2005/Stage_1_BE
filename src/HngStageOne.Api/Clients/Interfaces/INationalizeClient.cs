using HngStageOne.Api.DTOs.ExternalApis;

namespace HngStageOne.Api.Clients.Interfaces;

public interface INationalizeClient
{
    Task<NationalizeResponse?> GetNationalityAsync(string name);
}
