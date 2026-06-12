using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
    /// 身份认证服务接口
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="request">注册请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应，包含访问令牌等信息</returns>
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应，包含访问令牌等信息</returns>
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应</returns>
        Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    }
}