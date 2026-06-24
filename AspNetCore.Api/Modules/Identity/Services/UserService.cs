using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
    /// 用户服务实现类
    /// 提供租户用户管理的具体业务逻辑实现
    /// </summary>
    public sealed class UserService : IUserService
    {
        /// <summary>
        /// 数据库上下文
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /// <summary>
        /// 密码加密器
        /// </summary>
        private readonly IPasswordHasher _passwordHasher;

        /// <summary>
        /// 初始化用户服务
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="passwordHasher">密码加密器</param>
        public UserService(ApplicationDbContext dbContext, IPasswordHasher passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// 获取租户用户分页列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">用户查询请求（包含关键词筛选、状态筛选、分页参数）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>分页的用户列表响应，包含用户基本信息和所属角色</returns>
        public async Task<PagedResponse<UserListItemResponse>> GetTenantUsersAsync(Guid tenantId, UserQueryRequest request, CancellationToken cancellationToken)
        {
            var keyword = request.Keyword?.Trim();
            // 构建查询：关联租户用户表和用户表
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

            // 关键词筛选（用户名、显示名、邮箱）
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.UserName.Contains(keyword)
                    || x.DisplayName.Contains(keyword)
                    || x.Email.Contains(keyword));
            }

            // 状态筛选
            if (request.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == request.IsActive.Value);
            }

            // 分页处理
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.IsTenantOwner)
                .ThenBy(x => x.UserName)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            // 获取用户角色信息
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

        /// <summary>
        /// 获取用户详情
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应，包含角色和权限信息</returns>
        /// <exception cref="InvalidOperationException">用户未绑定到租户时抛出</exception>
        public async Task<UserProfileResponse> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            return await BuildUserProfileAsync(tenantId, userId, cancellationToken);
        }

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">创建用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>创建后的用户资料响应</returns>
        /// <exception cref="InvalidOperationException">租户不存在/禁用、用户名/邮箱重复或角色不属于租户时抛出</exception>
        public async Task<UserProfileResponse> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken cancellationToken)
        {
            var userName = request.UserName.Trim();
            var email = request.Email.Trim();
            var displayName = request.DisplayName.Trim();
            EnsureRequired(userName, "User name is required.");
            EnsureRequired(email, "Email is required.");
            EnsureRequired(displayName, "Display name is required.");

            // 验证租户存在且处于激活状态
            var tenantExists = await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
            if (!tenantExists)
            {
                throw new InvalidOperationException("Tenant does not exist or is disabled.");
            }

            // 检查用户名或邮箱是否重复
            var duplicateUser = await _dbContext.Users.AnyAsync(x => x.UserName == userName || x.Email == email, cancellationToken);
            if (duplicateUser)
            {
                throw new InvalidOperationException("User name or email already exists.");
            }

            // 验证角色ID属于当前租户
            var normalizedRoleIds = await ValidateRoleIdsAsync(tenantId, request.RoleIds, cancellationToken);
            var passwordResult = _passwordHasher.HashPassword(request.Password);
            var utcNow = DateTime.UtcNow;

            // 创建用户实体
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

            // 添加用户、租户用户关联和角色分配
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

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="request">更新用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>更新后的用户资料响应</returns>
        /// <exception cref="InvalidOperationException">用户未绑定到租户、用户不存在或邮箱重复时抛出</exception>
        public async Task<UserProfileResponse> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var email = request.Email.Trim();
            var displayName = request.DisplayName.Trim();
            EnsureRequired(email, "Email is required.");
            EnsureRequired(displayName, "Display name is required.");

            // 检查邮箱是否被其他用户占用
            var duplicatedEmail = await _dbContext.Users.AnyAsync(
                x => x.Id != user.Id && x.Email == email,
                cancellationToken);

            if (duplicatedEmail)
            {
                throw new InvalidOperationException("Email already exists.");
            }

            // 更新用户信息
            user.DisplayName = displayName;
            user.Email = email;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return await BuildUserProfileAsync(tenantId, userId, cancellationToken);
        }

        /// <summary>
        /// 删除用户（从租户中移除）
        /// 如果用户未绑定其他租户，则将用户标记为非活跃状态
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">要删除的用户ID</param>
        /// <param name="currentUserId">当前操作用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">当前用户不能删除自己、用户未绑定到租户或租户所有者不能被删除时抛出</exception>
        public async Task DeleteAsync(Guid tenantId, Guid userId, Guid currentUserId, CancellationToken cancellationToken)
        {
            // 禁止删除自己
            if (userId == currentUserId)
            {
                throw new InvalidOperationException("Current user cannot delete itself.");
            }

            var tenantUser = await _dbContext.TenantUsers
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException("User is not bound to the tenant.");

            // 禁止删除租户所有者
            if (tenantUser.IsTenantOwner)
            {
                throw new InvalidOperationException("Tenant owner cannot be deleted.");
            }

            // 删除用户角色分配
            var assignments = await _dbContext.UserRoles
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .ToListAsync(cancellationToken);

            _dbContext.UserRoles.RemoveRange(assignments);
            _dbContext.TenantUsers.Remove(tenantUser);

            // 检查用户是否绑定其他租户
            var hasOtherTenant = await _dbContext.TenantUsers
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.TenantId != tenantId, cancellationToken);

            // 如果未绑定其他租户，将用户标记为非活跃
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

        /// <summary>
        /// 为用户分配角色（替换现有角色）
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="roleIds">角色ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">用户未绑定到租户或角色不属于租户时抛出</exception>
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

        /// <summary>
        /// 重置用户密码（管理员操作，不需要旧密码）
        /// </summary>
        public async Task ResetPasswordAsync(Guid tenantId, Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
        {
            await EnsureTenantUserAsync(tenantId, userId, cancellationToken);
            var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var passwordResult = _passwordHasher.HashPassword(request.NewPassword);
            user.PasswordHash = passwordResult.Hash;
            user.PasswordSalt = passwordResult.Salt;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 验证角色ID是否属于指定租户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="roleIds">角色ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>去重后的角色ID集合</returns>
        /// <exception cref="InvalidOperationException">至少有一个角色不属于租户时抛出</exception>
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

        /// <summary>
        /// 确保用户已绑定到指定租户
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">用户未绑定到租户时抛出</exception>
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

        /// <summary>
        /// 构建用户资料响应
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应</returns>
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

        /// <summary>
        /// 获取用户的角色代码列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>角色代码列表</returns>
        private async Task<IReadOnlyCollection<string>> GetRoleCodesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.UserRoles.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .Join(_dbContext.Roles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Code)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 获取用户的权限代码列表
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>权限代码列表</returns>
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

        /// <summary>
        /// 获取用户角色查找表（批量获取多个用户的角色）
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="userIds">用户ID集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户ID到角色代码列表的字典</returns>
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

        /// <summary>
        /// 确保必填字段非空
        /// </summary>
        /// <param name="value">要验证的值</param>
        /// <param name="message">验证失败时的错误消息</param>
        /// <exception cref="InvalidOperationException">值为空时抛出</exception>
        private static void EnsureRequired(string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}