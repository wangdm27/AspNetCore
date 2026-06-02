using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public sealed class UserService : IUserService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;

        public UserService(ApplicationDbContext dbContext, IPasswordHasher passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<PagedResponse<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, UserQueryRequest request, CancellationToken cancellationToken)
        {
            var keyword = request.Keyword?.Trim();
            var query = from tenantUser in _dbContext.TenantUsers.AsNoTracking()
                        join user in _dbContext.Users.AsNoTracking()
                            on tenantUser.UserId equals user.Id
                        where tenantUser.TenantId == tenantId
                        select new
                        {
                            user.Id,
                            user.UserName,
                            user.DisplayName,
                            user.Email,
                            user.IsActive,
                            tenantUser.IsTenantOwner,
                            tenantUser.JoinedAt
                        };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.UserName.Contains(keyword)
                    || x.DisplayName.Contains(keyword)
                    || x.Email.Contains(keyword));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == request.IsActive.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.IsTenantOwner)
                .ThenBy(x => x.UserName)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var userIds = items.Select(x => x.Id).ToList();
            var userRoleLookup = await GetUserRoleLookupAsync(tenantId, userIds, cancellationToken);

            return new PagedResponse<UserListItemResponse>
            {
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                Items = items.Select(x => new UserListItemResponse
                {
                    UserId = x.Id,
                    UserName = x.UserName,
                    DisplayName = x.DisplayName,
                    Email = x.Email,
                    IsActive = x.IsActive,
                    IsTenantOwner = x.IsTenantOwner,
                    Roles = userRoleLookup.GetValueOrDefault(x.Id, Array.Empty<string>())
                }).ToList()
            };
        }

        public async Task<UserProfileResponse> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            return await BuildUserProfileAsync(tenantId, userId, cancellationToken);
        }

        public async Task<UserProfileResponse> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken)
        {
            var userName = request.UserName.Trim();
            var email = request.Email.Trim();
            var displayName = request.DisplayName.Trim();
            EnsureRequired(userName, "User name is required.");
            EnsureRequired(email, "Email is required.");
            EnsureRequired(displayName, "Display name is required.");

            var tenantExists = await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
            if (!tenantExists)
            {
                throw new InvalidOperationException("Tenant does not exist or is disabled.");
            }

            var duplicateUser = await _dbContext.Users.AnyAsync(x => x.UserName == userName || x.Email == email, cancellationToken);
            if (duplicateUser)
            {
                throw new InvalidOperationException("User name or email already exists.");
            }

            var normalizedRoleIds = await ValidateRoleIdsAsync(tenantId, request.RoleIds, cancellationToken);
            var passwordResult = _passwordHasher.HashPassword(request.Password);
            var utcNow = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                DisplayName = displayName,
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                IsActive = request.IsActive,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.TenantUsers.AddAsync(new TenantUser
            {
                TenantId = tenantId,
                UserId = user.Id,
                IsTenantOwner = false,
                JoinedAt = utcNow
            }, cancellationToken);
            await _dbContext.UserRoles.AddRangeAsync(normalizedRoleIds.Select(roleId => new UserRole
            {
                TenantId = tenantId,
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = utcNow
            }), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await BuildUserProfileAsync(tenantId, user.Id, cancellationToken);
        }

        public async Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var email = request.Email.Trim();
            var displayName = request.DisplayName.Trim();
            EnsureRequired(email, "Email is required.");
            EnsureRequired(displayName, "Display name is required.");

            var duplicatedEmail = await _dbContext.Users.AnyAsync(
                x => x.Id != user.Id && x.Email == email,
                cancellationToken);

            if (duplicatedEmail)
            {
                throw new InvalidOperationException("Email already exists.");
            }

            user.DisplayName = displayName;
            user.Email = email;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return await BuildUserProfileAsync(tenantId, userId, cancellationToken);
        }

        public async Task DeleteAsync(Guid tenantId, Guid userId, Guid currentUserId, CancellationToken cancellationToken)
        {
            if (userId == currentUserId)
            {
                throw new InvalidOperationException("Current user cannot delete itself.");
            }

            var tenantUser = await _dbContext.TenantUsers
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException("User is not bound to the tenant.");

            if (tenantUser.IsTenantOwner)
            {
                throw new InvalidOperationException("Tenant owner cannot be deleted.");
            }

            var assignments = await _dbContext.UserRoles
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .ToListAsync(cancellationToken);

            _dbContext.UserRoles.RemoveRange(assignments);
            _dbContext.TenantUsers.Remove(tenantUser);

            var hasOtherTenant = await _dbContext.TenantUsers
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.TenantId != tenantId, cancellationToken);

            if (!hasOtherTenant)
            {
                var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
                if (user is not null)
                {
                    user.IsActive = false;
                    user.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task AssignRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            var normalizedRoleIds = await ValidateRoleIdsAsync(tenantId, roleIds, cancellationToken);
            var currentAssignments = await _dbContext.UserRoles
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .ToListAsync(cancellationToken);

            _dbContext.UserRoles.RemoveRange(currentAssignments);

            var utcNow = DateTime.UtcNow;
            var newAssignments = normalizedRoleIds.Select(roleId => new UserRole
            {
                TenantId = tenantId,
                UserId = userId,
                RoleId = roleId,
                AssignedAt = utcNow
            });

            await _dbContext.UserRoles.AddRangeAsync(newAssignments, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<IReadOnlyCollection<Guid>> ValidateRoleIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        {
            var normalizedRoleIds = roleIds.Distinct().ToList();
            var existingRoles = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && normalizedRoleIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (existingRoles.Count != normalizedRoleIds.Count)
            {
                throw new InvalidOperationException("At least one role does not belong to the tenant.");
            }

            return normalizedRoleIds;
        }

        private async Task EnsureTenantUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            var tenantUserExists = await _dbContext.TenantUsers.AnyAsync(
                x => x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

            if (!tenantUserExists)
            {
                throw new InvalidOperationException("User is not bound to the tenant.");
            }
        }

        private async Task<UserProfileResponse> BuildUserProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");
            var tenant = await _dbContext.Tenants.AsNoTracking().SingleAsync(x => x.Id == tenantId, cancellationToken);
            var roleCodes = await GetRoleCodesAsync(tenantId, userId, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenantId, userId, cancellationToken);

            return new UserProfileResponse
            {
                UserId = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                TenantId = tenantId,
                TenantCode = tenant.Code,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        private async Task<IReadOnlyCollection<string>> GetRoleCodesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.UserRoles.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .Join(_dbContext.Roles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Code)
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

        private async Task<Dictionary<Guid, IReadOnlyCollection<string>>> GetUserRoleLookupAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        {
            if (userIds.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyCollection<string>>();
            }

            return await (from userRole in _dbContext.UserRoles.AsNoTracking()
                          join role in _dbContext.Roles.AsNoTracking()
                              on userRole.RoleId equals role.Id
                          where userRole.TenantId == tenantId && userIds.Contains(userRole.UserId)
                          select new
                          {
                              userRole.UserId,
                              role.Code
                          })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(
                    x => x.Key,
                    x => (IReadOnlyCollection<string>)x.Select(item => item.Code).Distinct().OrderBy(item => item).ToList(),
                    cancellationToken);
        }

        private static void EnsureRequired(string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
