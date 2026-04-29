namespace HngStageOne.Api.Models;

public class PagedResult<T>
{
    public required int Page { get; set; }
    public required int Limit { get; set; }
    public required int Total { get; set; }
    public required IReadOnlyList<T> Items { get; set; }
}
