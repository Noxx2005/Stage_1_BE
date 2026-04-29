using HngStageOne.Api.Clients.Interfaces;
using HngStageOne.Api.DTOs.ExternalApis;
using HngStageOne.Api.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HngStageOne.Api.Clients.Implementations;

public class AgifyClient : IAgifyClient
{
    private readonly HttpClient _httpClient;

    public AgifyClient(HttpClient httpClient, IOptions<ExternalApiOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.AgifyBaseUrl);
    }

    public async Task<AgifyResponse?> GetAgeAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync($"?name={Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AgifyResponse>(content);
            return result;
        }
        catch
        {
            return null;
        }
    }
}
