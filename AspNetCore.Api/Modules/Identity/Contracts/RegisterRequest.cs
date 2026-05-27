using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class RegisterRequest
    {
        [Required]
        [MaxLength(50)]
        public string TenantCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(32)]
        public string Password { get; set; } = string.Empty;
    }
}
