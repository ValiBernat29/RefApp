using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefApp.Models;

namespace RefApp.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await context.Database.MigrateAsync();

        string[] roleNames = { "Board", "Referee" };

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var boardEmail = "board@example.com";
        var boardUser = await userManager.FindByEmailAsync(boardEmail);

        if (boardUser == null)
        {
            boardUser = new ApplicationUser
            {
                UserName = boardEmail,
                Email = boardEmail,
                EmailConfirmed = true,
                DisplayName = "Board User"
            };

            var result = await userManager.CreateAsync(boardUser, "Board123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(boardUser, "Board");
            }
        }

        var refereeEmail = "referee@example.com";
        var refereeUser = await userManager.FindByEmailAsync(refereeEmail);

        if (refereeUser == null)
        {
            refereeUser = new ApplicationUser
            {
                UserName = refereeEmail,
                Email = refereeEmail,
                EmailConfirmed = true,
                DisplayName = "Referee User"
            };

            var result = await userManager.CreateAsync(refereeUser, "Referee123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(refereeUser, "Referee");
            }
        }
    }
}

