using HngStageOne.Api.DTOs.ExternalApis;

namespace HngStageOne.Api.Clients.Interfaces;

public interface IGenderizeClient
{
    Task<GenderizeResponse?> GetGenderAsync(string name);
}
