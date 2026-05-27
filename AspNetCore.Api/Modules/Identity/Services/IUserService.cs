using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, CancellationToken cancellationToken);

        Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

        Task AssignRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    }
}
