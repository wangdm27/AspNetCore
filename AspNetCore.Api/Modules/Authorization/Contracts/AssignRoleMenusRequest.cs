using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class AssignRoleMenusRequest
    {
        [Required]
        public IReadOnlyCollection<Guid> MenuPermissionIds { get; set; } = Array.Empty<Guid>();

        public IReadOnlyCollection<Guid> ButtonPermissionIds { get; set; } = Array.Empty<Guid>();
    }
}
