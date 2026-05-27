using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class LoginRequest
    {
        [Required]
        [MaxLength(50)]
        public string TenantCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        public string Password { get; set; } = string.Empty;
    }
}
