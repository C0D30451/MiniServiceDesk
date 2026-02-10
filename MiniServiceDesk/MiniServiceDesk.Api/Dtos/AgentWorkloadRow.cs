namespace MiniServiceDesk.Api.Dtos;

public sealed class AgentWorkloadRow
{
    public string AgentUserName { get; init; } = string.Empty;
    public int TotalAssigned { get; init; }
    public Dictionary<int, int> ByStatus { get; init; } = new();
}
