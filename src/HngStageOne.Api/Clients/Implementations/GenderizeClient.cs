using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.DTOs.ExternalApis;
using HngStageOne.Api.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HngStageOne.Api.Clients.Implementations;

public class GenderizeClient : IGenderizeClient
{
    private readonly HttpClient _httpClient;

    public GenderizeClient(HttpClient httpClient, IOptions<ExternalApiOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.GenderizeBaseUrl);
    }

    public async Task<GenderizeResponse?> GetGenderAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync($"?name={Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GenderizeResponse>(content);
            return result;
        }
        catch
        {
            return null;
        }
    }
}
