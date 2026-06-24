using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Tenancy.Contracts
{
    public sealed class UpdateTenantRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
