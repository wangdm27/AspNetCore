using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Tenancy.Contracts
{
    public sealed class AddTenantUserRequest
    {
        [Required]
        public Guid UserId { get; set; }

        public bool IsTenantOwner { get; set; }
    }
}
