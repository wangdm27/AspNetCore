using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public sealed class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;

        public AuthService(
            ApplicationDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .SingleOrDefaultAsync(x => x.Code == request.TenantCode, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            if (!tenant.IsActive)
            {
                throw new InvalidOperationException("Tenant is disabled.");
            }

            await EnsureUserNameAndEmailAreUniqueAsync(request.UserName, request.Email, cancellationToken);

            var passwordResult = _passwordHasher.HashPassword(request.Password);
            var utcNow = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.UserName.Trim(),
                Email = request.Email.Trim(),
                DisplayName = request.DisplayName.Trim(),
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                IsActive = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            var tenantUser = new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                IsTenantOwner = false,
                JoinedAt = utcNow
            };

            var defaultRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && x.IsDefault)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var userRoles = defaultRoleIds.Select(roleId => new UserRole
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = utcNow
            }).ToList();

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.TenantUsers.AddAsync(tenantUser, cancellationToken);
            await _dbContext.UserRoles.AddRangeAsync(userRoles, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await BuildAuthResponseAsync(user, tenant, cancellationToken);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .SingleOrDefaultAsync(x => x.Code == request.TenantCode, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken)
                ?? throw new InvalidOperationException("User name or password is invalid.");

            var tenantUser = await _dbContext.TenantUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenant.Id && x.UserId == user.Id,
                    cancellationToken);

            if (tenantUser is null || !tenant.IsActive || !user.IsActive)
            {
                throw new InvalidOperationException("Current user is not available in the tenant.");
            }

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                throw new InvalidOperationException("User name or password is invalid.");
            }

            return await BuildAuthResponseAsync(user, tenant, cancellationToken);
        }

        public async Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            var roleCodes = await GetRoleCodesAsync(tenantId, userId, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenantId, userId, cancellationToken);

            return new UserProfileResponse
            {
                UserId = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        private async Task<AuthResponse> BuildAuthResponseAsync(User user, Tenant tenant, CancellationToken cancellationToken)
        {
            var roleCodes = await GetRoleCodesAsync(tenant.Id, user.Id, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenant.Id, user.Id, cancellationToken);
            var tokenResult = _tokenService.CreateToken(user, tenant, roleCodes, permissionCodes);

            return new AuthResponse
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                AccessToken = tokenResult.AccessToken,
                ExpiresAt = tokenResult.ExpiresAt,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        private async Task<IReadOnlyCollection<string>> GetRoleCodesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .Join(_dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        private async Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return await (from userRole in _dbContext.UserRoles.AsNoTracking()
                          join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                              on userRole.RoleId equals rolePermission.RoleId
                          join permission in _dbContext.Permissions.AsNoTracking()
                              on rolePermission.PermissionId equals permission.Id
                          where userRole.TenantId == tenantId && userRole.UserId == userId
                          select permission.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        private async Task EnsureUserNameAndEmailAreUniqueAsync(string userName, string email, CancellationToken cancellationToken)
        {
            var normalizedUserName = userName.Trim();
            var normalizedEmail = email.Trim();
            var exists = await _dbContext.Users.AnyAsync(
                x => x.UserName == normalizedUserName || x.Email == normalizedEmail,
                cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException("User name or email already exists.");
            }
        }
    }
}
