using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
/// 身份认证服务实现类
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// 应用程序数据库上下文
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 密码哈希器
    /// </summary>
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>
    /// Token 服务
    /// </summary>
    private readonly ITokenService _tokenService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dbContext">应用程序数据库上下文</param>
    /// <param name="passwordHasher">密码哈希器</param>
    /// <param name="tokenService">Token 服务</param>
    public AuthService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="request">注册请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应，包含访问令牌等信息</returns>
        /// <exception cref="InvalidOperationException">当租户不存在、租户被禁用或用户名/邮箱已存在时抛出</exception>
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .SingleOrDefaultAsync(x => x.Code == request.TenantCode, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            if (!tenant.IsActive)
            {
                throw new InvalidOperationException("Tenant is disabled.");
            }

            await EnsureUserNameAndEmailAreUniqueAsync(request.UserName, request.Email, cancellationToken);

            var passwordResult = _passwordHasher.HashPassword(request.Password);
            var utcNow = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.UserName.Trim(),
                Email = request.Email.Trim(),
                DisplayName = request.DisplayName.Trim(),
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                IsActive = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            var tenantUser = new TenantUser
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                IsTenantOwner = false,
                JoinedAt = utcNow
            };

            var defaultRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && x.IsDefault)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var userRoles = defaultRoleIds.Select(roleId => new UserRole
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = utcNow
            }).ToList();

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.TenantUsers.AddAsync(tenantUser, cancellationToken);
            await _dbContext.UserRoles.AddRangeAsync(userRoles, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return await BuildAuthResponseAsync(user, tenant, cancellationToken);
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="request">登录请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应，包含访问令牌等信息</returns>
        /// <exception cref="InvalidOperationException">当租户不存在、用户名或密码无效、用户在租户中不可用时抛出</exception>
        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .SingleOrDefaultAsync(x => x.Code == request.TenantCode, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken)
                ?? throw new InvalidOperationException("User name or password is invalid.");

            var tenantUser = await _dbContext.TenantUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenant.Id && x.UserId == user.Id,
                    cancellationToken);

            if (tenantUser is null || !tenant.IsActive || !user.IsActive)
            {
                throw new InvalidOperationException("Current user is not available in the tenant.");
            }

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                throw new InvalidOperationException("User name or password is invalid.");
            }

            return await BuildAuthResponseAsync(user, tenant, cancellationToken);
        }

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="tenantId">租户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>用户资料响应，包含用户基本信息、角色和权限</returns>
        /// <exception cref="InvalidOperationException">当用户或租户不存在时抛出</exception>
        public async Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
                ?? throw new InvalidOperationException("Tenant does not exist.");

            var roleCodes = await GetRoleCodesAsync(tenantId, userId, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenantId, userId, cancellationToken);

            return new UserProfileResponse
            {
                UserId = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        /// <summary>
        /// 构建认证响应
        /// </summary>
        /// <param name="user">用户实体</param>
        /// <param name="tenant">租户实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>认证响应对象</returns>
        private async Task<AuthResponse> BuildAuthResponseAsync(User user, Tenant tenant, CancellationToken cancellationToken)
        {
            var roleCodes = await GetRoleCodesAsync(tenant.Id, user.Id, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenant.Id, user.Id, cancellationToken);
            var tokenResult = _tokenService.CreateToken(user, tenant, roleCodes, permissionCodes);

            return new AuthResponse
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                AccessToken = tokenResult.AccessToken,
                ExpiresAt = tokenResult.ExpiresAt,
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
        /// <returns>角色代码的只读集合</returns>
        private async Task<IReadOnlyCollection<string>> GetRoleCodesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == userId)
                .Join(_dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => role.Code)
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
        /// <returns>权限代码的只读集合</returns>
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
        /// 确保用户名和邮箱的唯一性
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="email">邮箱地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="InvalidOperationException">当用户名或邮箱已存在时抛出</exception>
        private async Task EnsureUserNameAndEmailAreUniqueAsync(string userName, string email, CancellationToken cancellationToken)
        {
            var normalizedUserName = userName.Trim();
            var normalizedEmail = email.Trim();
            var exists = await _dbContext.Users.AnyAsync(
                x => x.UserName == normalizedUserName || x.Email == normalizedEmail,
                cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException("User name or email already exists.");
            }
        }
    }
}