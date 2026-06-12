using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Tenancy.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Tenancy.Services
{
    /// <summary>
    /// 租户服务实现类
    /// 提供租户管理的具体业务逻辑实现
    /// </summary>
    public sealed class TenantService : ITenantService
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
        /// 初始化租户服务
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="passwordHasher">密码加密器</param>
        public TenantService(ApplicationDbContext dbContext, IPasswordHasher passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// 获取所有租户列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>租户响应列表，按创建时间升序排列</returns>
        public async Task<IReadOnlyList<TenantResponse>> GetAllAsync(CancellationToken cancellationToken)
        {
            var tenants = await _dbContext.Tenants
                .AsNoTracking()
                .OrderBy(x => x.CreatedAt)
                .Select(x => new TenantResponse
                {
                    TenantId = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return tenants;
        }

        /// <summary>
        /// 创建新租户
        /// 同时创建管理员用户、管理员角色、成员角色及其权限配置
        /// </summary>
        /// <param name="request">创建租户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>创建后的租户响应</returns>
        /// <exception cref="InvalidOperationException">租户代码已存在或管理员用户名/邮箱已存在时抛出</exception>
        public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
        {
            var normalizedCode = request.Code.Trim();
            var normalizedAdminUserName = request.AdminUserName.Trim();
            var normalizedAdminEmail = request.AdminEmail.Trim();

            // 检查租户代码是否已存在
            var tenantCodeExists = await _dbContext.Tenants.AnyAsync(x => x.Code == normalizedCode, cancellationToken);
            if (tenantCodeExists)
            {
                throw new InvalidOperationException("Tenant code already exists.");
            }

            // 检查管理员用户是否已存在
            var adminUserExists = await _dbContext.Users.AnyAsync(
                x => x.UserName == normalizedAdminUserName || x.Email == normalizedAdminEmail,
                cancellationToken);
            if (adminUserExists)
            {
                throw new InvalidOperationException("Admin user name or email already exists.");
            }

            var utcNow = DateTime.UtcNow;
            var passwordResult = _passwordHasher.HashPassword(request.AdminPassword);

            // 创建租户实体
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Code = normalizedCode,
                Name = request.Name.Trim(),
                IsActive = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            // 创建管理员用户实体
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                UserName = normalizedAdminUserName,
                Email = normalizedAdminEmail,
                DisplayName = request.AdminDisplayName.Trim(),
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                IsActive = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            // 创建管理员角色
            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "tenant_admin",
                Name = "Tenant Administrator",
                Description = "Full access inside the current tenant.",
                IsDefault = false,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            // 创建成员角色（默认角色）
            var memberRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Code = "tenant_member",
                Name = "Tenant Member",
                Description = "Default role for regular tenant users.",
                IsDefault = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            // 获取所有权限ID，用于管理员角色
            var permissionIds = await _dbContext.Permissions
                .AsNoTracking()
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var adminRolePermissions = permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permissionId,
                GrantedAt = utcNow
            }).ToList();

            // 获取成员角色的权限（仅menu.view和user.view）
            var memberPermissionIds = await _dbContext.Permissions
                .AsNoTracking()
                .Where(x => x.Code == "menu.view" || x.Code == "user.view")
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var memberRolePermissions = memberPermissionIds.Select(permissionId => new RolePermission
            {
                RoleId = memberRole.Id,
                PermissionId = permissionId,
                GrantedAt = utcNow
            }).ToList();

            // 添加所有实体到数据库
            await _dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await _dbContext.Users.AddAsync(adminUser, cancellationToken);
            await _dbContext.Roles.AddRangeAsync(new[] { adminRole, memberRole }, cancellationToken);
            await _dbContext.TenantUsers.AddAsync(new TenantUser
            {
                TenantId = tenant.Id,
                UserId = adminUser.Id,
                IsTenantOwner = true,
                JoinedAt = utcNow
            }, cancellationToken);

            await _dbContext.UserRoles.AddAsync(new UserRole
            {
                TenantId = tenant.Id,
                UserId = adminUser.Id,
                RoleId = adminRole.Id,
                AssignedAt = utcNow
            }, cancellationToken);

            await _dbContext.RolePermissions.AddRangeAsync(adminRolePermissions, cancellationToken);
            await _dbContext.RolePermissions.AddRangeAsync(memberRolePermissions, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new TenantResponse
            {
                TenantId = tenant.Id,
                Code = tenant.Code,
                Name = tenant.Name,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt
            };
        }

        /// <summary>
        /// 根据租户ID获取租户详情
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>租户响应</returns>
        /// <exception cref="InvalidOperationException">租户不存在时抛出</exception>
        public async Task<TenantResponse> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            return new TenantResponse
            {
                TenantId = tenant.Id,
                Code = tenant.Code,
                Name = tenant.Name,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt
            };
        }

        /// <summary>
        /// 向租户添加用户
        /// 同时为用户分配租户的默认角色
        /// </summary>
        /// <param name="tenantId">租户ID</param>
        /// <param name="request">添加租户用户请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">租户不存在、用户不存在或用户已绑定到租户时抛出</exception>
        public async Task AddUserAsync(Guid tenantId, AddTenantUserRequest request, CancellationToken cancellationToken)
        {
            // 检查租户是否存在
            var tenantExists = await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
            if (!tenantExists)
            {
                throw new InvalidOperationException("Tenant does not exist.");
            }

            // 检查用户是否存在
            var userExists = await _dbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException("User does not exist.");
            }

            // 检查用户是否已绑定到租户
            var relationExists = await _dbContext.TenantUsers.AnyAsync(
                x => x.TenantId == tenantId && x.UserId == request.UserId,
                cancellationToken);

            if (relationExists)
            {
                throw new InvalidOperationException("User is already bound to the tenant.");
            }

            var utcNow = DateTime.UtcNow;
            // 添加租户用户关联
            await _dbContext.TenantUsers.AddAsync(new TenantUser
            {
                TenantId = tenantId,
                UserId = request.UserId,
                IsTenantOwner = request.IsTenantOwner,
                JoinedAt = utcNow
            }, cancellationToken);

            // 获取租户的默认角色
            var defaultRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsDefault)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            // 为用户分配默认角色
            var roleAssignments = defaultRoleIds.Select(roleId => new UserRole
            {
                TenantId = tenantId,
                UserId = request.UserId,
                RoleId = roleId,
                AssignedAt = utcNow
            });

            await _dbContext.UserRoles.AddRangeAsync(roleAssignments, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}