using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Infrastructure.Services;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AspNetCore.Api.Modules.Identity.Services
{
    /// <summary>
    /// 身份认证服务实现类
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly JwtOptions _jwtOptions;

        public AuthService(
            ApplicationDbContext dbContext,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IEmailService emailService,
            IOptions<JwtOptions> jwtOptions)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _emailService = emailService;
            _jwtOptions = jwtOptions.Value;
        }

        /// <summary>
        /// 用户注册
        /// </summary>
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
        /// 刷新令牌
        /// </summary>
        public async Task<AuthResponse> RefreshTokenAsync(string refreshTokenValue, CancellationToken cancellationToken)
        {
            var tokenHash = ComputeSha256Hash(refreshTokenValue);

            var refreshToken = await _dbContext.RefreshTokens
                .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
                ?? throw new InvalidOperationException("Invalid refresh token.");

            if (refreshToken.IsUsed)
            {
                throw new InvalidOperationException("Refresh token has already been used.");
            }

            if (refreshToken.ExpiresAt < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Refresh token has expired.");
            }

            refreshToken.IsUsed = true;

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == refreshToken.UserId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            if (!user.IsActive)
            {
                throw new InvalidOperationException("User is disabled.");
            }

            var tenantUser = await _dbContext.TenantUsers
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Join(_dbContext.Tenants.AsNoTracking(), tu => tu.TenantId, t => t.Id, (tu, t) => new { tu, t })
                .Where(x => x.t.IsActive)
                .OrderByDescending(x => x.tu.JoinedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (tenantUser is null)
            {
                throw new InvalidOperationException("User has no active tenant.");
            }

            var tenant = tenantUser.t;
            var roleCodes = await GetRoleCodesAsync(tenant.Id, user.Id, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenant.Id, user.Id, cancellationToken);
            var tokenResult = _tokenService.CreateToken(user, tenant, roleCodes, permissionCodes);

            var newTokenHash = ComputeSha256Hash(tokenResult.RefreshToken);
            await _dbContext.RefreshTokens.AddAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiresDays),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AuthResponse
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresAt = tokenResult.ExpiresAt,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        public async Task ChangePasswordAsync(Guid userId, Guid tenantId, ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            if (!_passwordHasher.Verify(request.OldPassword, user.PasswordHash, user.PasswordSalt))
            {
                throw new InvalidOperationException("Old password is incorrect.");
            }

            var passwordResult = _passwordHasher.HashPassword(request.NewPassword);
            user.PasswordHash = passwordResult.Hash;
            user.PasswordSalt = passwordResult.Salt;
            user.UpdatedAt = DateTime.UtcNow;

            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && !x.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var token in activeRefreshTokens)
            {
                token.IsUsed = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 忘记密码：生成重置 token 并发送邮件
        /// </summary>
        public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Code == request.TenantCode, cancellationToken);

            if (tenant is null || !tenant.IsActive)
            {
                // 不暴露租户是否存在，统一返回成功
                return;
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Email == request.Email, cancellationToken);

            if (user is null || !user.IsActive)
            {
                // 不暴露用户是否存在
                return;
            }

            // 生成 JWT 短期重置 token（15 分钟有效）
            var resetToken = GeneratePasswordResetToken(user.Id, tenant.Id);
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken, cancellationToken);
        }

        /// <summary>
        /// 重置密码：验证 token 后设置新密码
        /// </summary>
        public async Task ResetPasswordAsync(ConfirmResetPasswordRequest request, CancellationToken cancellationToken)
        {
            var principal = ValidatePasswordResetToken(request.Token);
            if (principal is null)
            {
                throw new InvalidOperationException("Invalid or expired reset token.");
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var purposeClaim = principal.FindFirst("purpose")?.Value;

            if (userIdClaim is null || purposeClaim != "password_reset")
            {
                throw new InvalidOperationException("Invalid reset token.");
            }

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                throw new InvalidOperationException("Invalid reset token.");
            }

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == userId && x.Email == request.Email, cancellationToken)
                ?? throw new InvalidOperationException("User does not exist.");

            var passwordResult = _passwordHasher.HashPassword(request.NewPassword);
            user.PasswordHash = passwordResult.Hash;
            user.PasswordSalt = passwordResult.Salt;
            user.UpdatedAt = DateTime.UtcNow;

            // 撤销所有 Refresh Token
            var activeRefreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && !x.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var token in activeRefreshTokens)
            {
                token.IsUsed = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 构建认证响应（含 Refresh Token 存储）
        /// </summary>
        private async Task<AuthResponse> BuildAuthResponseAsync(User user, Tenant tenant, CancellationToken cancellationToken)
        {
            var roleCodes = await GetRoleCodesAsync(tenant.Id, user.Id, cancellationToken);
            var permissionCodes = await GetPermissionCodesAsync(tenant.Id, user.Id, cancellationToken);
            var tokenResult = _tokenService.CreateToken(user, tenant, roleCodes, permissionCodes);

            var tokenHash = ComputeSha256Hash(tokenResult.RefreshToken);
            await _dbContext.RefreshTokens.AddAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiresDays),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new AuthResponse
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                TenantCode = tenant.Code,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                ExpiresAt = tokenResult.ExpiresAt,
                Roles = roleCodes,
                Permissions = permissionCodes
            };
        }

        /// <summary>
        /// 生成密码重置 JWT Token（15 分钟有效）
        /// </summary>
        private string GeneratePasswordResetToken(Guid userId, Guid tenantId)
        {
            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("tenant_id", tenantId.ToString()),
                new("purpose", "password_reset"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(15),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 验证密码重置 Token
        /// </summary>
        private ClaimsPrincipal? ValidatePasswordResetToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                return new JwtSecurityTokenHandler().ValidateToken(
                    token, tokenValidationParameters, out var securityToken);
            }
            catch
            {
                return null;
            }
        }

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

        private static string ComputeSha256Hash(string rawValue)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
            return Convert.ToBase64String(hashBytes);
        }
    }
}