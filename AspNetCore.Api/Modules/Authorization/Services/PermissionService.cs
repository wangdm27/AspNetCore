using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    /// <summary>
    /// 权限服务实现类
    /// 提供权限查询、菜单获取和路由获取等功能
    /// </summary>
    public sealed class PermissionService : IPermissionService
    {
        /// <summary>
        /// 应用程序数据库上下文
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbContext">应用程序数据库上下文</param>
        public PermissionService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取所有权限列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>权限响应列表，按权限类型和代码排序</returns>
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

        /// <summary>
        /// 获取当前用户的路由列表
        /// 路由获取逻辑与菜单相同，直接复用 GetCurrentMenusAsync 方法
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>路由响应列表</returns>
        public Task<IReadOnlyList<MenuResponse>> GetCurrentRoutesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return GetCurrentMenusAsync(tenantId, userId, cancellationToken);
        }

        /// <summary>
        /// 获取当前用户的菜单列表
        /// 根据用户的角色权限过滤出可访问的菜单，并构建菜单树结构
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>菜单响应列表（树形结构）</returns>
        public async Task<IReadOnlyList<MenuResponse>> GetCurrentMenusAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            // 查询用户拥有的所有权限（通过角色关联）
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

            // 转换为内部权限记录
            var grantedPermissions = permissions
                .Select(x => new GrantedPermission(x.Id, x.Code, x.Name, x.Type))
                .ToList();

            // 提取权限代码集合（用于快速查找）和按钮权限列表
            var permissionCodes = grantedPermissions.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var buttonPermissions = grantedPermissions
                .Where(x => x.Type == PermissionType.Button || x.Type == PermissionType.Api)
                .OrderBy(x => x.Code)
                .ToList();

            // 获取所有菜单
            var menus = await _dbContext.Menus
                .AsNoTracking()
                .OrderBy(x => x.Sort)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            // 根据权限过滤可见菜单
            var visibleMenus = menus
                .Where(x => string.IsNullOrWhiteSpace(x.PermissionCode) || permissionCodes.Contains(x.PermissionCode))
                .ToList();

            // 构建树形菜单结构
            return BuildMenuTree(visibleMenus, buttonPermissions, null);
        }

        /// <summary>
        /// 递归构建菜单树
        /// </summary>
        /// <param name="menus">所有菜单列表</param>
        /// <param name="buttonPermissions">按钮权限列表</param>
        /// <param name="parentId">父菜单ID（null 表示顶级菜单）</param>
        /// <returns>菜单响应列表（树形结构）</returns>
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
                    Children = BuildMenuTree(menus, buttonPermissions, x.Id) // 递归构建子菜单
                })
                .ToList();
        }

        /// <summary>
        /// 解析菜单对应的按钮权限
        /// 根据菜单权限代码的前缀匹配按钮权限
        /// </summary>
        /// <param name="menuPermissionCode">菜单权限代码</param>
        /// <param name="buttonPermissions">所有按钮权限列表</param>
        /// <returns>菜单关联的按钮权限列表</returns>
        private static IReadOnlyList<MenuButtonResponse> ResolveButtons(string menuPermissionCode, IReadOnlyCollection<GrantedPermission> buttonPermissions)
        {
            // 如果菜单没有权限代码或格式不正确，返回空列表
            if (string.IsNullOrWhiteSpace(menuPermissionCode) || !menuPermissionCode.Contains('.'))
            {
                return Array.Empty<MenuButtonResponse>();
            }

            // 提取权限代码的区域前缀（如 "system.user."）
            var areaPrefix = menuPermissionCode[..(menuPermissionCode.IndexOf('.') + 1)];
            // 查找属于同一区域的按钮权限
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

        /// <summary>
        /// 授予的权限记录（内部使用）
        /// </summary>
        /// <param name="Id">权限ID</param>
        /// <param name="Code">权限代码</param>
        /// <param name="Name">权限名称</param>
        /// <param name="Type">权限类型</param>
        private sealed record GrantedPermission(Guid Id, string Code, string Name, PermissionType Type);
    }
}