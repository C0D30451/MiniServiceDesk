using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.models;

public class TicketAttachment
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string StoredRelativePath { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long FileSizeBytes { get; set; }

    public string? UploadedByUserId { get; set; }

    [MaxLength(80)]
    public string? UploadedByUserName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket? Ticket { get; set; }
}
