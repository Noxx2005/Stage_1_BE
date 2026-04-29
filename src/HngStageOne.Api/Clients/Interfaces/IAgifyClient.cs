using HngStageOne.Api.DTOs.ExternalApis;

namespace HngStageOne.Api.Clients.Interfaces;

public interface IAgifyClient
{
    Task<AgifyResponse?> GetAgeAsync(string name);
}
