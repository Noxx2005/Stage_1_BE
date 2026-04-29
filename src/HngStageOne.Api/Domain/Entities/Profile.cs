namespace HngStageOne.Api.Domain.Entities;

public class Profile
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Gender { get; set; }
    public required double GenderProbability { get; set; }
    public required int Age { get; set; }
    public required string AgeGroup { get; set; }
    public required string CountryId { get; set; }
    public required string CountryName { get; set; }
    public required double CountryProbability { get; set; }
    public required DateTime CreatedAt { get; set; }
}
