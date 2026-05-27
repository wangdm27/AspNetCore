using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Identity.Services
{
    public sealed class UserService : IUserService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var userRoleLookup = await (from userRole in _dbContext.UserRoles.AsNoTracking()
                                        join role in _dbContext.Roles.AsNoTracking()
                                            on userRole.RoleId equals role.Id
                                        where userRole.TenantId == tenantId
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

            var items = await (from tenantUser in _dbContext.TenantUsers.AsNoTracking()
                               join user in _dbContext.Users.AsNoTracking()
                                   on tenantUser.UserId equals user.Id
                               where tenantUser.TenantId == tenantId
                               orderby user.UserName
                               select new
                               {
                                   user.Id,
                                   user.UserName,
                                   user.DisplayName,
                                   user.Email,
                                   user.IsActive,
                                   tenantUser.IsTenantOwner
                               })
                .ToListAsync(cancellationToken);

            return items.Select(x => new UserListItemResponse
            {
                UserId = x.Id,
                UserName = x.UserName,
                DisplayName = x.DisplayName,
                Email = x.Email,
                IsActive = x.IsActive,
                IsTenantOwner = x.IsTenantOwner,
                Roles = userRoleLookup.GetValueOrDefault(x.Id, Array.Empty<string>())
            }).ToList();
        }

        public async Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
        {
            var tenantUser = await _dbContext.TenantUsers
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException("User is not bound to the tenant.");

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == tenantUser.UserId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var duplicatedEmail = await _dbContext.Users.AnyAsync(
                x => x.Id != user.Id && x.Email == request.Email.Trim(),
                cancellationToken);

            if (duplicatedEmail)
            {
                throw new InvalidOperationException("Email already exists.");
            }

            user.DisplayName = request.DisplayName.Trim();
            user.Email = request.Email.Trim();
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var roleCodes = await (from userRole in _dbContext.UserRoles.AsNoTracking()
                                   join role in _dbContext.Roles.AsNoTracking()
                                       on userRole.RoleId equals role.Id
                                   where userRole.TenantId == tenantId && userRole.UserId == userId
                                   select role.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            var permissionCodes = await (from userRole in _dbContext.UserRoles.AsNoTracking()
                                         join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                             on userRole.RoleId equals rolePermission.RoleId
                                         join permission in _dbContext.Permissions.AsNoTracking()
                                             on rolePermission.PermissionId equals permission.Id
                                         where userRole.TenantId == tenantId && userRole.UserId == userId
                                         select permission.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            var tenant = await _dbContext.Tenants.AsNoTracking()
                .SingleAsync(x => x.Id == tenantId, cancellationToken);

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

        public async Task AssignRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
        {
            var tenantUserExists = await _dbContext.TenantUsers.AnyAsync(
                x => x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

            if (!tenantUserExists)
            {
                throw new InvalidOperationException("User is not bound to the tenant.");
            }

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
    }
}
