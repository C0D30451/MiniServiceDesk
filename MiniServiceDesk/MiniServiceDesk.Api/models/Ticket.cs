using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniServiceDesk.Api.models;

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Waiting = 2,
    Resolved = 3,
    Closed = 4
}

public class Ticket
{
    public int Id { get; set; }

    [Required]
    [MinLength(4)]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MinLength(10)]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Category { get; set; } = "IT";

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public string? AssignedToUserId { get; set; }

    [MaxLength(80)]
    public string? AssignedToUserName { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public List<TicketComment> Comments { get; set; } = new();
}
