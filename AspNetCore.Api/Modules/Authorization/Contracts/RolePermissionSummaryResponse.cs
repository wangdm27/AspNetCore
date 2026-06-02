namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class RolePermissionSummaryResponse
    {
        public IReadOnlyCollection<Guid> PermissionIds { get; set; } = Array.Empty<Guid>();

        public IReadOnlyCollection<Guid> MenuPermissionIds { get; set; } = Array.Empty<Guid>();

        public IReadOnlyCollection<Guid> ButtonPermissionIds { get; set; } = Array.Empty<Guid>();

        public IReadOnlyCollection<Guid> ApiPermissionIds { get; set; } = Array.Empty<Guid>();
    }
}
