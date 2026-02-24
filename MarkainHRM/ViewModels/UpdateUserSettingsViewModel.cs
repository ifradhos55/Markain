using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.ViewModels
{
    public class UpdateUserSettingsViewModel
    {
        // Profile Picture
        public IFormFile? ProfilePictureFile { get; set; }
        public string? ProfilePictureUrl { get; set; }

        // Username Change
        [Display(Name = "New Username")]
        public string? NewUsername { get; set; }

        // Password Change
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Display(Name = "Confirm New Password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmNewPassword { get; set; }
    }
}
