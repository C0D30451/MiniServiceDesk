namespace MiniServiceDesk.Api.Dtos;

public sealed class NotificationRow
{
    public int Id { get; init; }
    public int? TicketId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NotificationType { get; init; } = "general";
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}
