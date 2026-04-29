namespace HngStageOne.Api.Helpers.Exceptions;

public class InvalidUpstreamResponseException : Exception
{
    public string ExternalApiName { get; }

    public InvalidUpstreamResponseException(string externalApiName, string? message = null)
        : base(message ?? $"{externalApiName} returned an invalid response")
    {
        ExternalApiName = externalApiName;
    }
}
