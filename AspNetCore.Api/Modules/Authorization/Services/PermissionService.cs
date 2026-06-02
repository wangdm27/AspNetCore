using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    public sealed class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _dbContext;

        public PermissionService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.Permissions
                .AsNoTracking()
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Code)
                .Select(x => new PermissionResponse
                {
                    PermissionId = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Type = x.Type.ToString(),
                    Description = x.Description,
                    HttpMethod = x.HttpMethod,
                    Route = x.Route
                })
                .ToListAsync(cancellationToken);
        }

        public Task<IReadOnlyList<MenuResponse>> GetCurrentRoutesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return GetCurrentMenusAsync(tenantId, userId, cancellationToken);
        }

        public async Task<IReadOnlyList<MenuResponse>> GetCurrentMenusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            var permissions = await (from userRole in _dbContext.UserRoles.AsNoTracking()
                                     join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                         on userRole.RoleId equals rolePermission.RoleId
                                     join permission in _dbContext.Permissions.AsNoTracking()
                                         on rolePermission.PermissionId equals permission.Id
                                     where userRole.TenantId == tenantId && userRole.UserId == userId
                                     select new
                                     {
                                         permission.Id,
                                         permission.Code,
                                         permission.Name,
                                         permission.Type
                                     })
                .Distinct()
                .ToListAsync(cancellationToken);

            var grantedPermissions = permissions
                .Select(x => new GrantedPermission(x.Id, x.Code, x.Name, x.Type))
                .ToList();

            var permissionCodes = grantedPermissions.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var buttonPermissions = grantedPermissions
                .Where(x => x.Type == PermissionType.Button || x.Type == PermissionType.Api)
                .OrderBy(x => x.Code)
                .ToList();

            var menus = await _dbContext.Menus
                .AsNoTracking()
                .OrderBy(x => x.Sort)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var visibleMenus = menus
                .Where(x => string.IsNullOrWhiteSpace(x.PermissionCode) || permissionCodes.Contains(x.PermissionCode))
                .ToList();

            return BuildMenuTree(visibleMenus, buttonPermissions, null);
        }

        private static IReadOnlyList<MenuResponse> BuildMenuTree(
            IReadOnlyCollection<DataAccess.Entities.Menu> menus,
            IReadOnlyCollection<GrantedPermission> buttonPermissions,
            Guid? parentId)
        {
            return menus
                .Where(x => x.ParentId == parentId)
                .OrderBy(x => x.Sort)
                .ThenBy(x => x.Name)
                .Select(x => new MenuResponse
                {
                    MenuId = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Path = x.Path,
                    Component = x.Component,
                    Icon = x.Icon,
                    Sort = x.Sort,
                    PermissionCode = x.PermissionCode,
                    Buttons = ResolveButtons(x.PermissionCode, buttonPermissions),
                    Children = BuildMenuTree(menus, buttonPermissions, x.Id)
                })
                .ToList();
        }

        private static IReadOnlyList<MenuButtonResponse> ResolveButtons(string menuPermissionCode, IReadOnlyCollection<GrantedPermission> buttonPermissions)
        {
            if (string.IsNullOrWhiteSpace(menuPermissionCode) || !menuPermissionCode.Contains('.'))
            {
                return Array.Empty<MenuButtonResponse>();
            }

            var areaPrefix = menuPermissionCode[..(menuPermissionCode.IndexOf('.') + 1)];
            return buttonPermissions
                .Where(x => x.Code.StartsWith(areaPrefix, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.Code, menuPermissionCode, StringComparison.OrdinalIgnoreCase))
                .Select(x => new MenuButtonResponse
                {
                    PermissionId = x.Id,
                    Code = x.Code,
                    Name = x.Name
                })
                .ToList();
        }

        private sealed record GrantedPermission(Guid Id, string Code, string Name, PermissionType Type);
    }
}
