namespace AspNetCore.Api.Infrastructure.Extensions
{
    /// <summary>
    /// HttpContext 扩展方法类
    /// 提供从 HttpContext 中提取认证信息的便捷方法
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// 从 HttpContext 中获取必需的用户ID
        /// </summary>
        /// <param name="httpContext">HttpContext 实例</param>
        /// <returns>用户ID（Guid）</returns>
        /// <exception cref="InvalidOperationException">当无法从 Claims 中解析出有效的用户ID时抛出</exception>
        public static Guid GetRequiredUserId(this HttpContext httpContext)
        {
            // 从 Claims 中查找用户ID（使用标准的 NameIdentifier Claim 类型）
            var rawUserId = httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (Guid.TryParse(rawUserId, out var userId))
            {
                return userId;
            }

            throw new InvalidOperationException("Authenticated user id is required.");
        }

        /// <summary>
        /// 从 HttpContext 中获取必需的租户ID
        /// 优先从 Claims 中获取，如果不存在则从请求头 X-Tenant-Id 中获取
        /// </summary>
        /// <param name="httpContext">HttpContext 实例</param>
        /// <returns>租户ID（Guid）</returns>
        /// <exception cref="InvalidOperationException">当无法解析出有效的租户ID时抛出</exception>
        public static Guid GetRequiredTenantId(this HttpContext httpContext)
        {
            // 优先从 Claims 中获取租户ID，如果不存在则从请求头中获取
            var rawTenantId = httpContext.User.FindFirst("tenant_id")?.Value
                ?? httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (Guid.TryParse(rawTenantId, out var tenantId))
            {
                return tenantId;
            }

            throw new InvalidOperationException("Tenant id is required.");
        }
    }
}