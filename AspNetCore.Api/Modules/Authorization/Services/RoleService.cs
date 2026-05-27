using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    public sealed class RoleService : IRoleService
    {
        private readonly ApplicationDbContext _dbContext;

        public RoleService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<RoleResponse> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken)
        {
            var normalizedCode = request.Code.Trim();
            var normalizedName = request.Name.Trim();

            var duplicatedRole = await _dbContext.Roles.AnyAsync(
                x => x.TenantId == tenantId && (x.Code == normalizedCode || x.Name == normalizedName),
                cancellationToken);

            if (duplicatedRole)
            {
                throw new InvalidOperationException("Role code or name already exists in the tenant.");
            }

            var utcNow = DateTime.UtcNow;
            var role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = normalizedCode,
                Name = normalizedName,
                Description = request.Description.Trim(),
                IsDefault = request.IsDefault,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            await _dbContext.Roles.AddAsync(role, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RoleResponse
            {
                RoleId = role.Id,
                TenantId = role.TenantId,
                Code = role.Code,
                Name = role.Name,
                Description = role.Description,
                IsDefault = role.IsDefault,
                Permissions = Array.Empty<string>()
            };
        }

        public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var roles = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var rolePermissions = await (from rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                         join permission in _dbContext.Permissions.AsNoTracking()
                                             on rolePermission.PermissionId equals permission.Id
                                         join role in _dbContext.Roles.AsNoTracking()
                                             on rolePermission.RoleId equals role.Id
                                         where role.TenantId == tenantId
                                         select new
                                         {
                                             role.Id,
                                             permission.Code
                                         })
                .GroupBy(x => x.Id)
                .ToDictionaryAsync(
                    x => x.Key,
                    x => (IReadOnlyCollection<string>)x.Select(item => item.Code).Distinct().OrderBy(item => item).ToList(),
                    cancellationToken);

            return roles.Select(role => new RoleResponse
            {
                RoleId = role.Id,
                TenantId = role.TenantId,
                Code = role.Code,
                Name = role.Name,
                Description = role.Description,
                IsDefault = role.IsDefault,
                Permissions = rolePermissions.GetValueOrDefault(role.Id, Array.Empty<string>())
            }).ToList();
        }

        public async Task AssignPermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
        {
            var role = await _dbContext.Roles
                .SingleOrDefaultAsync(x => x.Id == roleId && x.TenantId == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Role does not belong to the tenant.");

            var normalizedPermissionIds = permissionIds.Distinct().ToList();
            var existingPermissionIds = await _dbContext.Permissions
                .AsNoTracking()
                .Where(x => normalizedPermissionIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (existingPermissionIds.Count != normalizedPermissionIds.Count)
            {
                throw new InvalidOperationException("At least one permission does not exist.");
            }

            var currentPermissions = await _dbContext.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .ToListAsync(cancellationToken);

            _dbContext.RolePermissions.RemoveRange(currentPermissions);

            var utcNow = DateTime.UtcNow;
            var newPermissions = normalizedPermissionIds.Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
                GrantedAt = utcNow
            });

            await _dbContext.RolePermissions.AddRangeAsync(newPermissions, cancellationToken);
            role.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
