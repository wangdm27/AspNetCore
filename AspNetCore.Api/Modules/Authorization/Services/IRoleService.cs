using AspNetCore.Api.Modules.Authorization.Contracts;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    /// <summary>
    /// 角色服务接口
    /// 提供角色的创建、查询、权限分配等功能
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">创建角色请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色响应</returns>
        Task<RoleResponse> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 获取租户下的所有角色列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色响应列表</returns>
        Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken);

        /// <summary>
        /// 获取角色的权限摘要
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色权限摘要响应</returns>
        Task<RolePermissionSummaryResponse> GetRolePermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);

        /// <summary>
        /// 为角色分配权限
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="permissionIds">权限ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task AssignPermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken);

        /// <summary>
        /// 为角色分配菜单权限
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="request">分配菜单请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task AssignMenusAsync(Guid tenantId, Guid roleId, AssignRoleMenusRequest request, CancellationToken cancellationToken);
    }
}