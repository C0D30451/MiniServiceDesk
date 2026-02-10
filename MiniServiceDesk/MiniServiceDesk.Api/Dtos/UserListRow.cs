namespace MiniServiceDesk.Api.Dtos;

public sealed class UserListRow
{
    public string UserName { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
}
