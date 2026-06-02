using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public interface IUserService
    {
        Task<PagedResponse<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, UserQueryRequest request, CancellationToken cancellationToken);

        Task<UserProfileResponse> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

        Task<UserProfileResponse> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken);

        Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

        Task DeleteAsync(Guid tenantId, Guid userId, Guid currentUserId, CancellationToken cancellationToken);

        Task AssignRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    }
}
