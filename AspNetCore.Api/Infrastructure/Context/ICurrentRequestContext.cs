namespace AspNetCore.Api.Infrastructure.Context
{
    public interface ICurrentRequestContext
    {
        Guid? UserId { get; }

        Guid? TenantId { get; }

        string? UserName { get; }

        string? TenantCode { get; }

        bool IsAuthenticated { get; }
    }
}
