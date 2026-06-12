namespace AspNetCore.Api.Infrastructure.Context
{
    /// <summary>
    /// 当前请求上下文接口
    /// 提供当前请求的用户和租户信息
    /// </summary>
    public interface ICurrentRequestContext
    {
        /// <summary>
        /// 当前用户ID
        /// </summary>
        Guid? UserId { get; }

        /// <summary>
        /// 当前租户ID
        /// </summary>
        Guid? TenantId { get; }

        /// <summary>
        /// 当前用户名
        /// </summary>
        string? UserName { get; }

        /// <summary>
        /// 当前租户代码
        /// </summary>
        string? TenantCode { get; }

        /// <summary>
        /// 是否已认证
        /// </summary>
        bool IsAuthenticated { get; }
    }
}