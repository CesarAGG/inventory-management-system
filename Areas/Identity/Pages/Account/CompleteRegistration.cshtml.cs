using InventoryManagementSystem.Data;
using InventoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class CompleteRegistrationModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public CompleteRegistrationModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public CompleteRegistrationViewModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null || !info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
            {
                // Highly unlikely case, but handle it to satisfy null analysis
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var suggestedUserName = Regex.Replace(email.Split('@')[0], @"[^a-zA-Z0-9_.-]", "");

            Input = new CompleteRegistrationViewModel
            {
                Email = email,
                UserName = suggestedUserName
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = Input.UserName, Email = Input.Email, EmailConfirmed = true };
                var result = await _userManager.CreateAsync(user);

                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(ReturnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            Input.Email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            return Page();
        }
    }
}