using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using AspNetCore.DataAccess.Entities.Enums;
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

        public async Task<RolePermissionSummaryResponse> GetRolePermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        {
            await EnsureRoleAsync(tenantId, roleId, cancellationToken);

            var permissions = await (from rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                     join permission in _dbContext.Permissions.AsNoTracking()
                                         on rolePermission.PermissionId equals permission.Id
                                     where rolePermission.RoleId == roleId
                                     select new { permission.Id, permission.Type })
                .ToListAsync(cancellationToken);

            return new RolePermissionSummaryResponse
            {
                PermissionIds = permissions.Select(x => x.Id).ToList(),
                MenuPermissionIds = permissions.Where(x => x.Type == PermissionType.Menu).Select(x => x.Id).ToList(),
                ButtonPermissionIds = permissions.Where(x => x.Type == PermissionType.Button).Select(x => x.Id).ToList(),
                ApiPermissionIds = permissions.Where(x => x.Type == PermissionType.Api).Select(x => x.Id).ToList()
            };
        }

        public async Task AssignPermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
        {
            var role = await EnsureRoleAsync(tenantId, roleId, cancellationToken);
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
            await AddRolePermissionsAsync(role, normalizedPermissionIds, cancellationToken);
        }

        public async Task AssignMenusAsync(Guid tenantId, Guid roleId, AssignRoleMenusRequest request, CancellationToken cancellationToken)
        {
            var role = await EnsureRoleAsync(tenantId, roleId, cancellationToken);
            var menuPermissionIds = request.MenuPermissionIds.Distinct().ToList();
            var buttonPermissionIds = request.ButtonPermissionIds.Distinct().ToList();
            var requestedIds = menuPermissionIds.Concat(buttonPermissionIds).Distinct().ToList();

            var permissions = await _dbContext.Permissions
                .AsNoTracking()
                .Where(x => requestedIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Type })
                .ToListAsync(cancellationToken);

            if (permissions.Count != requestedIds.Count
                || permissions.Any(x => x.Type != PermissionType.Menu && x.Type != PermissionType.Button)
                || permissions.Count(x => x.Type == PermissionType.Menu) != menuPermissionIds.Count
                || permissions.Count(x => x.Type == PermissionType.Button) != buttonPermissionIds.Count)
            {
                throw new InvalidOperationException("Only existing menu and button permissions can be assigned by this endpoint.");
            }

            var currentMenuAndButtons = await _dbContext.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .Join(_dbContext.Permissions, rolePermission => rolePermission.PermissionId, permission => permission.Id, (rolePermission, permission) => new { RolePermission = rolePermission, permission.Type })
                .Where(x => x.Type == PermissionType.Menu || x.Type == PermissionType.Button)
                .Select(x => x.RolePermission)
                .ToListAsync(cancellationToken);

            _dbContext.RolePermissions.RemoveRange(currentMenuAndButtons);
            await AddRolePermissionsAsync(role, requestedIds, cancellationToken);
        }

        private async Task AddRolePermissionsAsync(Role role, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var newPermissions = permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permissionId,
                GrantedAt = utcNow
            });

            await _dbContext.RolePermissions.AddRangeAsync(newPermissions, cancellationToken);
            role.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<Role> EnsureRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        {
            return await _dbContext.Roles
                .SingleOrDefaultAsync(x => x.Id == roleId && x.TenantId == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Role does not belong to the tenant.");
        }
    }
}
