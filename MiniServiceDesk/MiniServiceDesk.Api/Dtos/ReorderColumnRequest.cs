namespace MiniServiceDesk.Api.Dtos;

public sealed class ReorderColumnRequest
{
    public int? TicketColumnId { get; init; }
    public List<int> OrderedTicketIds { get; init; } = new();
}
