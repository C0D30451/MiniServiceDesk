namespace MiniServiceDesk.Api.Dtos;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string[] Roles { get; init; } = Array.Empty<string>();
}
