using Microsoft.AspNetCore.Identity;

namespace MiniServiceDesk.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var roles = new[] { "User", "Agent", "Admin" };
        foreach (var role in roles)
        {
            if (!await roleMgr.RoleExistsAsync(role))
            {
                await roleMgr.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUser(userMgr, "demo.user", "Passw0rd!", "User");
        await EnsureUser(userMgr, "demo.agent", "Passw0rd!", "Agent");
    }

    private static async Task EnsureUser(UserManager<IdentityUser> userMgr, string username, string password, string role)
    {
        var user = await userMgr.FindByNameAsync(username);
        if (user is null)
        {
            user = new IdentityUser { UserName = username };
            var result = await userMgr.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await userMgr.IsInRoleAsync(user, role))
        {
            await userMgr.AddToRoleAsync(user, role);
        }
    }
}
