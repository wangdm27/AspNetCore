using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class ResetPasswordRequest
    {
        [Required]
        [MinLength(6)]
        [MaxLength(32)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
