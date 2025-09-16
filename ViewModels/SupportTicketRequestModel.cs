using System.ComponentModel.DataAnnotations;

namespace InventoryManagementSystem.ViewModels;

public class SupportTicketRequestModel
{
    [Required]
    [StringLength(500, ErrorMessage = "The summary cannot exceed 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public string Priority { get; set; } = string.Empty;

    [Required]
    [Url]
    public string SourceUrl { get; set; } = string.Empty;

    public string? InventoryId { get; set; }
}