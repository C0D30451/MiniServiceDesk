using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.models;

public class TicketEvent
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    [MaxLength(40)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(600)]
    public string Message { get; set; } = string.Empty;

    public string? ActorUserId { get; set; }

    [MaxLength(80)]
    public string? ActorUserName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
