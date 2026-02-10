using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.models;

public class TicketColumn
{
    public int Id { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string OwnerUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string OwnerUserName { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
