using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
    /// 用户服务接口
    /// 提供租户用户管理相关的业务操作方法
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 获取租户用户分页列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">用户查询请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>分页的用户列表响应</returns>
        Task<PagedResponse<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, UserQueryRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 获取用户详情
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应</returns>
        Task<UserProfileResponse> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">创建用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>创建后的用户资料响应</returns>
        Task<UserProfileResponse> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="request">更新用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>更新后的用户资料响应</returns>
        Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">要删除的用户ID</param>
        /// <param name="currentUserId">当前操作用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task DeleteAsync(Guid tenantId, Guid userId, Guid currentUserId, CancellationToken cancellationToken);

        /// <summary>
        /// 为用户分配角色
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="roleIds">角色ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task AssignRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    }
}