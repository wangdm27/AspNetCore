using AspNetCore.Api.Modules.Authorization.Contracts;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    /// <summary>
    /// 角色服务接口
    /// </summary>
    public interface IRoleService
    {
        Task<RoleResponse> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken);

        Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken);

        Task<RoleResponse> UpdateAsync(Guid tenantId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken);

        Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

        Task<RolePermissionSummaryResponse> GetRolePermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

        Task AssignPermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken);

        Task AssignMenusAsync(Guid tenantId, Guid roleId, AssignRoleMenusRequest request, CancellationToken cancellationToken);
    }
}
