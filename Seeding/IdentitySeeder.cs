
using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankAppAPI.Seeding
{
    public static class IdentitySeeder
    {
        // Is called during application startup to seed default roles and admin user
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();   // Create a new scope to get scoped services

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            //Takes values from appsettings.json
            var adminSection = configuration.GetSection("Seed:Admin");
            var adminEmail = adminSection.GetValue<string>("Email");
            var adminPassword = adminSection.GetValue<string>("Password");
            var roles = adminSection.GetSection("Roles").Get<string[]>() ?? Array.Empty<string>();


            //Creates default role "Customer" if it does not exist, this is for when the program is run for the first time
            var requiredRoles = new[] {"Customer" };

            foreach (var role in requiredRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }


            // 1) Creates roles (admin) if they do not exist
            foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var createRole = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!createRole.Succeeded)
                    {
                        var errors = string.Join(", ", createRole.Errors.Select(e => $"{e.Code}:{e.Description}"));
                        throw new InvalidOperationException($"Role could not be created '{role}': {errors}");
                    }
                }
            }

            // 2) Creates admin user if it does not exist (diffrent from admin role)
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true // valfritt
                };

                var createUser = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createUser.Succeeded)
                {
                    var errors = string.Join(", ", createUser.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new InvalidOperationException($"Could not create role '{adminEmail}': {errors}");
                }
            }

            // 3) Ensures that the admin user has all the required roles For autorization
            var currentRoles = await userManager.GetRolesAsync(adminUser);
            var missingRoles = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missingRoles.Any())
            {
                var addRoles = await userManager.AddToRolesAsync(adminUser, missingRoles);
                if (!addRoles.Succeeded)
                {
                    var errors = string.Join(", ", addRoles.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new InvalidOperationException($"Role could not be created '{adminEmail}': {errors}");
                }
            }
        }
    }
}

