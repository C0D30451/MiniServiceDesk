using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Dtos;

public sealed class TicketDetailsResponse
{
    public Ticket Ticket { get; init; } = new();
    public List<TicketComment> Comments { get; init; } = new();
}
