using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
    /// 身份认证服务接口
    /// 提供用户注册、登录、令牌刷新、密码管理等身份认证相关功能
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="request">注册请求参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应（包含访问令牌等信息）</returns>
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应（包含访问令牌等信息）</returns>
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应</returns>
        Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);

        /// <summary>
        /// 刷新访问令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新的认证响应</returns>
        Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">修改密码请求参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task ChangePasswordAsync(Guid userId, Guid tenantId, ChangePasswordRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 忘记密码（发送密码重置链接）
        /// </summary>
        /// <param name="request">忘记密码请求参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="request">重置密码请求参数（包含验证码和新密码）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task ResetPasswordAsync(ConfirmResetPasswordRequest request, CancellationToken cancellationToken);
    }
}