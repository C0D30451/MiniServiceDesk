using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniServiceDesk.Api.models;

public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string AuthorUserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Ticket? Ticket { get; set; }
}
