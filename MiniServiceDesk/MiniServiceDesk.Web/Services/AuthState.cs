namespace MiniServiceDesk.Web.Services;

public sealed class AuthState
{
    public string? Token { get; private set; }
    public string? UserName { get; private set; }
    public string[] Roles { get; private set; } = Array.Empty<string>();

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

    public void Set(string token, string userName, string[] roles)
    {
        Token = token;
        UserName = userName;
        Roles = roles;
    }

    public void Clear()
    {
        Token = null;
        UserName = null;
        Roles = Array.Empty<string>();
    }

    public bool IsInRole(string role) => Roles.Contains(role);
}
