namespace MiniServiceDesk.Api.Dtos;

public sealed class ChangeStatusRequest
{
    public int NewStatus { get; init; }
    public string? Comment { get; init; }
}
