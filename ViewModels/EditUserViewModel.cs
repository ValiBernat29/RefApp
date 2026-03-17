using System.ComponentModel.DataAnnotations;

namespace RefApp.ViewModels;

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Display(Name = "Display Name")]
    public string? DisplayName { get; set; }

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    // --- NEW PROPERTY ---
    [Display(Name = "New Password (Optional)")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }
}