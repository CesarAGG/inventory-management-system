using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels
{
    public class CompleteRegistrationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.")]
        [RegularExpression("^[a-zA-Z0-9_.-]*$", ErrorMessage = "Username can only contain letters, numbers, and the characters _ . -")]
        [Display(Name = "Username")]
        public string UserName { get; set; }
    }
}