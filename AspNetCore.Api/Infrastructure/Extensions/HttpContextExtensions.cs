namespace AspNetCore.Api.Infrastructure.Extensions
{
    public static class HttpContextExtensions
    {
        public static Guid GetRequiredUserId(this HttpContext httpContext)
        {
            var rawUserId = httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (Guid.TryParse(rawUserId, out var userId))
            {
                return userId;
            }

            throw new InvalidOperationException("Authenticated user id is required.");
        }

        public static Guid GetRequiredTenantId(this HttpContext httpContext)
        {
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
