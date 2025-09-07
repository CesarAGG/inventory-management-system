using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels;

public class RenameInventoryRequest
{
    [Required]
    [StringLength(100, ErrorMessage = "The inventory name cannot exceed 100 characters.")]
    public string NewName { get; set; } = string.Empty;
}