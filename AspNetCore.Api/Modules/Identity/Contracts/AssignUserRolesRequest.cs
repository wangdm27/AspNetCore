using System.ComponentModel.DataAnnotations;

namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class AssignUserRolesRequest
    {
        [Required]
        public IReadOnlyCollection<Guid> RoleIds { get; set; } = Array.Empty<Guid>();
    }
}
