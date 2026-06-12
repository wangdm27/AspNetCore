using AspNetCore.Api.Modules.Authorization.Contracts;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    /// <summary>
    /// 权限服务接口
    /// 提供权限查询、菜单获取和路由获取等功能
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 获取所有权限列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>权限响应列表</returns>
        Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 获取当前用户的菜单列表
        /// 根据租户ID和用户ID获取该用户有权限访问的菜单
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>菜单响应列表</returns>
        Task<IReadOnlyList<MenuResponse>> GetCurrentMenusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

        /// <summary>
        /// 获取当前用户的路由列表
        /// 根据租户ID和用户ID获取该用户有权限访问的路由（API端点）
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>路由响应列表</returns>
        Task<IReadOnlyList<MenuResponse>> GetCurrentRoutesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    }
}