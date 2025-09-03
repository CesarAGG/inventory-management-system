using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Data;

public class CustomSignInManager : SignInManager<ApplicationUser>
{
    public CustomSignInManager(UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        // The user must not be blocked
        if (user.IsBlocked)
        {
            Logger.LogWarning(2, "User {userId} is blocked and cannot sign in.", await UserManager.GetUserIdAsync(user));
            return false;
        }

        // Also run the base checks (e.g., for email confirmation if it were enabled)
        return await base.CanSignInAsync(user);
    }
}