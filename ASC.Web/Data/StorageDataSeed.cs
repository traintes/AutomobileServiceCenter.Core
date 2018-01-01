using ASC.Web.Configuration;
using ASC.Web.Models;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Data
{
    public interface IIdentitySeed
    {
        Task Seed(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            IOptions<ApplicationSettings> options);
    }

    public class IdentitySeed : IIdentitySeed
    {
        public async Task Seed(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, IOptions<ApplicationSettings> options)
        {
            string[] roles = options.Value.Roles.Split(',');

            // Create roles if they don't exist
            foreach (string role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    IdentityRole storageRole = new IdentityRole
                    {
                        Name = role,
                    };
                    IdentityResult roleResult = await roleManager.CreateAsync(storageRole);
                }
            }

            // Create admin if he doesn't exist
            ApplicationUser admin = await userManager.FindByEmailAsync(options.Value.AdminEmail);
            if (admin == null)
            {
                ApplicationUser user = new ApplicationUser
                {
                    UserName = options.Value.AdminName,
                    Email = options.Value.AdminEmail,
                    EmailConfirmed = true,
                };

                IdentityResult result = await userManager.CreateAsync(user, options.Value.AdminPassword);
                await userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", options.Value.AdminEmail));
                await userManager.AddClaimAsync(user, new System.Security.Claims.Claim("IsActive", "True"));

                // Add Admin to Admin roles
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "Admin");
            }
        }
    }
}
