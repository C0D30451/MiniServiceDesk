namespace MiniServiceDesk.Api.Dtos;

public sealed class LoginRequest
{
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
