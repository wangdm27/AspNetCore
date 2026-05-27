using AspNetCore.Api.Modules.Tenancy.Contracts;

namespace AspNetCore.Api.Modules.Tenancy.Services
{
    public interface ITenantService
    {
        Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);

        Task<TenantResponse> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken);

        Task AddUserAsync(Guid tenantId, AddTenantUserRequest request, CancellationToken cancellationToken);
    }
}
