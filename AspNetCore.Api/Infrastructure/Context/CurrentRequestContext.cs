using System.Security.Claims;

namespace AspNetCore.Api.Infrastructure.Context
{
    /// <summary>
    /// 当前请求上下文实现类
    /// 从 HTTP 上下文获取当前请求的用户和租户信息
    /// 支持从 Claims 和请求头两种方式获取租户信息
    /// </summary>
    public sealed class CurrentRequestContext : ICurrentRequestContext
    {
        /// <summary>
        /// HTTP 上下文访问器
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// 初始化当前请求上下文
        /// </summary>
        /// <param name="httpContextAccessor">HTTP 上下文访问器</param>
        public CurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 当前用户ID
        /// 从 Claims 中的 NameIdentifier 获取
        /// </summary>
        public Guid? UserId => TryParseGuid(GetClaimValue(ClaimTypes.NameIdentifier));

        /// <summary>
        /// 当前租户ID
        /// 优先从 Claims 的 tenant_id 获取，其次从请求头 X-Tenant-Id 获取
        /// </summary>
        public Guid? TenantId
        {
            get
            {
                var claimTenantId = TryParseGuid(GetClaimValue("tenant_id"));
                if (claimTenantId.HasValue)
                {
                    return claimTenantId;
                }

                var headerValue = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                return TryParseGuid(headerValue);
            }
        }

        /// <summary>
        /// 当前用户名
        /// 从 Claims 中的 Name 获取
        /// </summary>
        public string? UserName => GetClaimValue(ClaimTypes.Name);

        /// <summary>
        /// 当前租户代码
        /// 优先从 Claims 的 tenant_code 获取，其次从请求头 X-Tenant-Code 获取
        /// </summary>
        public string? TenantCode =>
            GetClaimValue("tenant_code")
            ?? _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Code"].FirstOrDefault();

        /// <summary>
        /// 是否已认证
        /// 判断当前用户是否已通过身份验证
        /// </summary>
        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        /// <summary>
        /// 获取指定类型的 Claim 值
        /// </summary>
        /// <param name="claimType">Claim 类型</param>
        /// <returns>Claim 值，如果不存在则返回 null</returns>
        private string? GetClaimValue(string claimType)
        {
            return _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        }

        /// <summary>
        /// 安全解析 Guid 字符串
        /// </summary>
        /// <param name="rawValue">原始字符串值</param>
        /// <returns>解析后的 Guid，如果解析失败则返回 null</returns>
        private static Guid? TryParseGuid(string? rawValue)
        {
            return Guid.TryParse(rawValue, out var value) ? value : null;
        }
    }
}