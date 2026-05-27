using System.Security.Claims;

namespace AspNetCore.Api.Infrastructure.Context
{
    public sealed class CurrentRequestContext : ICurrentRequestContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId => TryParseGuid(GetClaimValue(ClaimTypes.NameIdentifier));

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

        public string? UserName => GetClaimValue(ClaimTypes.Name);

        public string? TenantCode =>
            GetClaimValue("tenant_code")
            ?? _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Code"].FirstOrDefault();

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        private string? GetClaimValue(string claimType)
        {
            return _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        }

        private static Guid? TryParseGuid(string? rawValue)
        {
            return Guid.TryParse(rawValue, out var value) ? value : null;
        }
    }
}
