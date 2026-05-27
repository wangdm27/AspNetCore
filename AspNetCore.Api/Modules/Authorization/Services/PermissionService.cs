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
                    Type = x.Type == PermissionType.Api ? "Api" : "Menu",
                    Description = x.Description,
                    HttpMethod = x.HttpMethod,
                    Route = x.Route
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MenuResponse>> GetCurrentMenusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            var permissionCodes = await (from userRole in _dbContext.UserRoles.AsNoTracking()
                                         join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                             on userRole.RoleId equals rolePermission.RoleId
                                         join permission in _dbContext.Permissions.AsNoTracking()
                                             on rolePermission.PermissionId equals permission.Id
                                         where userRole.TenantId == tenantId && userRole.UserId == userId
                                         select permission.Code)
                .Distinct()
                .ToListAsync(cancellationToken);

            var menus = await _dbContext.Menus
                .AsNoTracking()
                .OrderBy(x => x.Sort)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var visibleMenus = menus
                .Where(x => string.IsNullOrWhiteSpace(x.PermissionCode) || permissionCodes.Contains(x.PermissionCode))
                .ToList();

            return BuildMenuTree(visibleMenus, null);
        }

        private static IReadOnlyList<MenuResponse> BuildMenuTree(IReadOnlyCollection<DataAccess.Entities.Menu> menus, Guid? parentId)
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
                    Children = BuildMenuTree(menus, x.Id)
                })
                .ToList();
        }
    }
}
