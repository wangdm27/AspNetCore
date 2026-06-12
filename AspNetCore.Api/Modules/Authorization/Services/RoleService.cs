using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using AspNetCore.DataAccess.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    /// <summary>
    /// 角色服务实现类
    /// 提供角色的创建、查询、权限分配等功能
    /// </summary>
    public sealed class RoleService : IRoleService
    {
        /// <summary>
        /// 应用程序数据库上下文
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbContext">应用程序数据库上下文</param>
        public RoleService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">创建角色请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色响应</returns>
        /// <exception cref="InvalidOperationException">当角色代码或名称在租户中已存在时抛出</exception>
        public async Task<RoleResponse> CreateAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken)
        {
            var normalizedCode = request.Code.Trim();
            var normalizedName = request.Name.Trim();

            // 检查角色代码或名称是否重复
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

        /// <summary>
        /// 获取租户下的所有角色列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色响应列表（包含每个角色的权限代码）</returns>
        public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            // 查询租户下的所有角色
            var roles = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            // 查询每个角色的权限代码
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

            // 构建响应列表
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

        /// <summary>
        /// 获取角色的权限摘要
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色权限摘要响应（按权限类型分类）</returns>
        /// <exception cref="InvalidOperationException">当角色不属于该租户时抛出</exception>
        public async Task<RolePermissionSummaryResponse> GetRolePermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        {
            // 验证角色是否属于该租户
            await EnsureRoleAsync(tenantId, roleId, cancellationToken);

            // 查询角色的所有权限
            var permissions = await (from rolePermission in _dbContext.RolePermissions.AsNoTracking()
                                     join permission in _dbContext.Permissions.AsNoTracking()
                                         on rolePermission.PermissionId equals permission.Id
                                     where rolePermission.RoleId == roleId
                                     select new { permission.Id, permission.Type })
                .ToListAsync(cancellationToken);

            // 按权限类型分类返回
            return new RolePermissionSummaryResponse
            {
                PermissionIds = permissions.Select(x => x.Id).ToList(),
                MenuPermissionIds = permissions.Where(x => x.Type == PermissionType.Menu).Select(x => x.Id).ToList(),
                ButtonPermissionIds = permissions.Where(x => x.Type == PermissionType.Button).Select(x => x.Id).ToList(),
                ApiPermissionIds = permissions.Where(x => x.Type == PermissionType.Api).Select(x => x.Id).ToList()
            };
        }

        /// <summary>
        /// 为角色分配权限
        /// 先清除现有权限，再分配新权限
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="permissionIds">权限ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">当角色不属于该租户或权限不存在时抛出</exception>
        public async Task AssignPermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken)
        {
            var role = await EnsureRoleAsync(tenantId, roleId, cancellationToken);
            var normalizedPermissionIds = permissionIds.Distinct().ToList();

            // 验证所有权限ID是否存在
            var existingPermissionIds = await _dbContext.Permissions
                .AsNoTracking()
                .Where(x => normalizedPermissionIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (existingPermissionIds.Count != normalizedPermissionIds.Count)
            {
                throw new InvalidOperationException("At least one permission does not exist.");
            }

            // 删除角色现有的所有权限
            var currentPermissions = await _dbContext.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .ToListAsync(cancellationToken);

            _dbContext.RolePermissions.RemoveRange(currentPermissions);
            await AddRolePermissionsAsync(role, normalizedPermissionIds, cancellationToken);
        }

        /// <summary>
        /// 为角色分配菜单权限
        /// 仅支持菜单和按钮类型的权限
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="request">分配菜单请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">当角色不属于该租户或权限类型不正确时抛出</exception>
        public async Task AssignMenusAsync(Guid tenantId, Guid roleId, AssignRoleMenusRequest request, CancellationToken cancellationToken)
        {
            var role = await EnsureRoleAsync(tenantId, roleId, cancellationToken);
            var menuPermissionIds = request.MenuPermissionIds.Distinct().ToList();
            var buttonPermissionIds = request.ButtonPermissionIds.Distinct().ToList();
            var requestedIds = menuPermissionIds.Concat(buttonPermissionIds).Distinct().ToList();

            // 验证权限是否存在且类型正确
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

            // 删除角色现有的菜单和按钮权限
            var currentMenuAndButtons = await _dbContext.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .Join(_dbContext.Permissions, rolePermission => rolePermission.PermissionId, permission => permission.Id, (rolePermission, permission) => new { RolePermission = rolePermission, permission.Type })
                .Where(x => x.Type == PermissionType.Menu || x.Type == PermissionType.Button)
                .Select(x => x.RolePermission)
                .ToListAsync(cancellationToken);

            _dbContext.RolePermissions.RemoveRange(currentMenuAndButtons);
            await AddRolePermissionsAsync(role, requestedIds, cancellationToken);
        }

        /// <summary>
        /// 添加角色权限
        /// </summary>
        /// <param name="role">角色实体</param>
        /// <param name="permissionIds">权限ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
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

        /// <summary>
        /// 验证角色是否属于指定租户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色实体</returns>
        /// <exception cref="InvalidOperationException">当角色不属于该租户时抛出</exception>
        private async Task<Role> EnsureRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
        {
            return await _dbContext.Roles
                .SingleOrDefaultAsync(x => x.Id == roleId && x.TenantId == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Role does not belong to the tenant.");
        }
    }
}