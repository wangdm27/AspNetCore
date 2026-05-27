using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class CreateRoleRequest
    {
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
    }
}
