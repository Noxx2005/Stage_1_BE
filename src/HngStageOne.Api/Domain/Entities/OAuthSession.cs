namespace HngStageOne.Api.Domain.Entities;

public class OAuthSession
{
    public Guid Id { get; set; }
    public required string State { get; set; }
    public required string CodeVerifier { get; set; }
    public required string RedirectUri { get; set; }
    public required string ClientType { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string? TokenResultJson { get; set; }
}
