using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Tenancy.Contracts
{
    public sealed class CreateTenantRequest
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AdminUserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string AdminEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AdminDisplayName { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        [MaxLength(32)]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
