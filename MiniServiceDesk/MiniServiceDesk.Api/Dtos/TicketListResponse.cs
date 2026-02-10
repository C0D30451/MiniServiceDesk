using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Dtos;

public sealed class TicketListResponse
{
    public List<Ticket> Items { get; init; } = new();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
