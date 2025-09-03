using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        // 1. Get the required services
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // 2. Seed the "Admin" role
        const string adminRoleName = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRoleName));
        }

        // 3. Read admin credentials from configuration
        var adminEmail = configuration["AdminCredentials:Email"];
        var adminPassword = configuration["AdminCredentials:Password"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            throw new InvalidOperationException("Admin credentials are not configured. Please set AdminCredentials:Email and AdminCredentials:Password in your configuration.");
        }

        // 4. Check if the admin user exists
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            // 5. Create the admin user if they don't exist
            var newAdminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(newAdminUser, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdminUser, adminRoleName);
            }
        }
    }
}