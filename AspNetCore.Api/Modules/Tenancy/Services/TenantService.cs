using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Modules.Tenancy.Contracts;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Tenancy.Services
{
    public sealed class TenantService : ITenantService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;

        public TenantService(ApplicationDbContext dbContext, IPasswordHasher passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
        {
            var normalizedCode = request.Code.Trim();
            var normalizedAdminUserName = request.AdminUserName.Trim();
            var normalizedAdminEmail = request.AdminEmail.Trim();

            var tenantCodeExists = await _dbContext.Tenants.AnyAsync(x => x.Code == normalizedCode, cancellationToken);
            if (tenantCodeExists)
            {
                throw new InvalidOperationException("Tenant code already exists.");
            }

            var adminUserExists = await _dbContext.Users.AnyAsync(
                x => x.UserName == normalizedAdminUserName || x.Email == normalizedAdminEmail,
                cancellationToken);
            if (adminUserExists)
            {
                throw new InvalidOperationException("Admin user name or email already exists.");
            }

            var utcNow = DateTime.UtcNow;
            var passwordResult = _passwordHasher.HashPassword(request.AdminPassword);

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Code = normalizedCode,
                Name = request.Name.Trim(),
                IsActive = true,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

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

        public async Task AddUserAsync(Guid tenantId, AddTenantUserRequest request, CancellationToken cancellationToken)
        {
            var tenantExists = await _dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
            if (!tenantExists)
            {
                throw new InvalidOperationException("Tenant does not exist.");
            }

            var userExists = await _dbContext.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                throw new InvalidOperationException("User does not exist.");
            }

            var relationExists = await _dbContext.TenantUsers.AnyAsync(
                x => x.TenantId == tenantId && x.UserId == request.UserId,
                cancellationToken);

            if (relationExists)
            {
                throw new InvalidOperationException("User is already bound to the tenant.");
            }

            var utcNow = DateTime.UtcNow;
            await _dbContext.TenantUsers.AddAsync(new TenantUser
            {
                TenantId = tenantId,
                UserId = request.UserId,
                IsTenantOwner = request.IsTenantOwner,
                JoinedAt = utcNow
            }, cancellationToken);

            var defaultRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsDefault)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

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
