using AspNetCore.Api.Modules.Identity.Contracts;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

        Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
    }
}
