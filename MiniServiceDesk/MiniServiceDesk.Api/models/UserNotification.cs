using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.models;

public class UserNotification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string UserName { get; set; } = string.Empty;

    public int? TicketId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(60)]
    public string NotificationType { get; set; } = "general";

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}
