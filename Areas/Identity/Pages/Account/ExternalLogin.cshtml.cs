using InventoryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Policy;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [TempData]
        public string? ErrorMessage { get; set; }

        // This handler is called by the provider button on the Login page
        public IActionResult OnPost(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        // This handler is called when the user returns from the external provider
        public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                ErrorMessage = "Email claim not received from external provider. Cannot proceed.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && user.IsBlocked)
            {
                _logger.LogWarning("Blocked user {email} attempted external login.", email);
                return RedirectToPage("./Lockout");
            }

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User {email} logged in with {provider} provider.", email, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // User does not exist or external login is not associated yet.
                if (user == null)
                {
                    user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                    var createUserResult = await _userManager.CreateAsync(user);
                    if (!createUserResult.Succeeded)
                    {
                        ErrorMessage = createUserResult.Errors.First().Description;
                        return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                    }
                }

                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded)
                {
                    ErrorMessage = addLoginResult.Errors.First().Description;
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                _logger.LogInformation("User account for {email} was created/linked and logged in with {provider}.", email, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
        }
    }
}