using AspNetCore.Api.Modules.Tenancy.Contracts;

namespace AspNetCore.Api.Modules.Tenancy.Services
{
    /// <summary>
    /// 租户服务接口
    /// 提供租户管理相关的业务操作方法
    /// </summary>
    public interface ITenantService
    {
        /// <summary>
        /// 获取所有租户列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>租户响应列表</returns>
        Task<IReadOnlyList<TenantResponse>> GetAllAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 创建新租户
        /// </summary>
        /// <param name="request">创建租户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>创建后的租户响应</returns>
        Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 根据租户ID获取租户详情
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>租户响应</returns>
        Task<TenantResponse> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken);

        /// <summary>
        /// 向租户添加用户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">添加租户用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task AddUserAsync(Guid tenantId, AddTenantUserRequest request, CancellationToken cancellationToken);
    }
}