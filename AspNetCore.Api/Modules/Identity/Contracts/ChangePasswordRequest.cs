using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class ChangePasswordRequest
    {
        [Required]
        [MinLength(6)]
        [MaxLength(32)]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(32)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
