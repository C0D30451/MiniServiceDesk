namespace MiniServiceDesk.Api.Dtos;

public sealed class DashboardSummaryResponse
{
    public int Total { get; init; }
    public int Unassigned { get; init; }
    public Dictionary<int, int> ByStatus { get; init; } = new();
    public Dictionary<int, int> ByPriority { get; init; } = new();
}
