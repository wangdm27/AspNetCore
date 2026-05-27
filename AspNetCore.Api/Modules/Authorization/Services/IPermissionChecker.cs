namespace AspNetCore.Api.Modules.Authorization.Services
{
    public interface IPermissionChecker
    {
        Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionCode, CancellationToken cancellationToken);
    }
}
