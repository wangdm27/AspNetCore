using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class AssignRolePermissionsRequest
    {
        [Required]
        public IReadOnlyCollection<Guid> PermissionIds { get; set; } = Array.Empty<Guid>();
    }
}
