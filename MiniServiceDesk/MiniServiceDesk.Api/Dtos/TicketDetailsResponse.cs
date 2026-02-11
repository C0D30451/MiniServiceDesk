using MiniServiceDesk.Api.models;

namespace MiniServiceDesk.Api.Dtos;

public sealed class TicketDetailsResponse
{
    public Ticket Ticket { get; init; } = new();
    public List<TicketComment> Comments { get; init; } = new();
    public List<TicketEventRow> Events { get; init; } = new();
    public List<TicketAttachmentRow> Attachments { get; init; } = new();
}

public sealed class TicketEventRow
{
    public int Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ActorUserName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class TicketAttachmentRow
{
    public int Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSizeBytes { get; init; }
    public string? UploadedByUserName { get; init; }
    public DateTime CreatedAt { get; init; }
}
