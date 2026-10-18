using BookingSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Data;

public static class AdministratorAccount
{
    public static async Task GrantAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email.Trim())
            ?? throw new InvalidOperationException("Account not found. Register it in the application first.");
        if (!await roles.RoleExistsAsync("Administrator"))
            Check(await roles.CreateAsync(new IdentityRole("Administrator")));
        if (!await users.IsInRoleAsync(user, "Administrator"))
            Check(await users.AddToRoleAsync(user, "Administrator"));
        Console.WriteLine("Administrator role granted. Sign out and sign in again to refresh permissions.");
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Unable to grant administrator role: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}
