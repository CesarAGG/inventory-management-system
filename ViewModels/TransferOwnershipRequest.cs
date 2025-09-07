using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels;

public class TransferOwnershipRequest
{
    [Required]
    [EmailAddress]
    public string NewOwnerEmail { get; set; } = string.Empty;
}