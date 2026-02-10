namespace MiniServiceDesk.Api.Dtos;

public sealed class TicketListQuery
{
    public string? Search { get; init; }
    public int? Status { get; init; }
    public int? Priority { get; init; }
    public string? AssignedTo { get; init; }
    public bool? UnassignedOnly { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public string? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
