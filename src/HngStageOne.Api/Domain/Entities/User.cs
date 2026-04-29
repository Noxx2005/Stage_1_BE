namespace HngStageOne.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public long GitHubId { get; set; }
    public required string GitHubUsername { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
