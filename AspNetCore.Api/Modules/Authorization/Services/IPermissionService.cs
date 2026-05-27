using AspNetCore.Api.Modules.Authorization.Contracts;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    public interface IPermissionService
    {
        Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<MenuResponse>> GetCurrentMenusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    }
}
