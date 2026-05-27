using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using AspNetCore.DataAccess.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    public sealed class AuthorizationSeedService : IAuthorizationSeedService
    {
        private static readonly Guid TenantManagePermissionId = Guid.Parse("1f54aa9f-e416-496d-b674-6fe730bb89f5");
        private static readonly Guid TenantViewPermissionId = Guid.Parse("4ef8dd49-8a89-4059-9000-bf165706ce2a");
        private static readonly Guid TenantUserAddPermissionId = Guid.Parse("c394bc3d-ee96-424d-b4c0-f85745d94db0");
        private static readonly Guid UserViewPermissionId = Guid.Parse("7f04b6c4-867f-4e72-bf1a-4e4df808ca1e");
        private static readonly Guid UserUpdatePermissionId = Guid.Parse("b6b44afe-48ec-49a3-86d9-031041f68334");
        private static readonly Guid UserAssignRolesPermissionId = Guid.Parse("8ec5036c-0678-434e-b3aa-3e631fbde4fd");
        private static readonly Guid RoleViewPermissionId = Guid.Parse("73b035aa-2118-48b5-a385-c349b3212f52");
        private static readonly Guid RoleCreatePermissionId = Guid.Parse("f7300fda-e08b-4c86-a2b9-d08a8cfc0131");
        private static readonly Guid RoleAssignPermissionId = Guid.Parse("01df4aa4-6f71-430d-b053-f4ca3331d6bf");
        private static readonly Guid PermissionViewPermissionId = Guid.Parse("576304a1-053f-439a-b89c-bf8499f64b8a");
        private static readonly Guid MenuViewPermissionId = Guid.Parse("e1e931ff-a11b-440a-a6b6-f6a8a1eb230f");

        private static readonly Guid TenantRootMenuId = Guid.Parse("0d21e602-86fc-4247-bf98-98eec40d1dac");
        private static readonly Guid UserRootMenuId = Guid.Parse("69c31d4a-e6ec-4776-9314-e1ccaa62e01d");
        private static readonly Guid RoleRootMenuId = Guid.Parse("15790478-9e85-4809-b1f0-5ff62636ed84");
        private static readonly Guid PermissionRootMenuId = Guid.Parse("390a53c9-eb62-4a8d-b14b-c52a66c38a53");

        private readonly ApplicationDbContext _dbContext;

        public AuthorizationSeedService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SeedAsync(CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var permissions = new[]
            {
                new Permission { Id = TenantManagePermissionId, Code = "tenant.create", Name = "Create Tenant", Type = PermissionType.Api, Description = "Create tenant and its initial administrator.", HttpMethod = "POST", Route = "/api/tenancy/tenants", CreatedAt = utcNow },
                new Permission { Id = TenantViewPermissionId, Code = "tenant.view", Name = "View Tenant", Type = PermissionType.Api, Description = "View current tenant profile.", HttpMethod = "GET", Route = "/api/tenancy/tenants/current", CreatedAt = utcNow },
                new Permission { Id = TenantUserAddPermissionId, Code = "tenant.user.add", Name = "Add Tenant User", Type = PermissionType.Api, Description = "Bind an existing user to a tenant.", HttpMethod = "POST", Route = "/api/tenancy/tenants/current/users", CreatedAt = utcNow },
                new Permission { Id = UserViewPermissionId, Code = "user.view", Name = "View Users", Type = PermissionType.Api, Description = "List tenant users.", HttpMethod = "GET", Route = "/api/identity/users", CreatedAt = utcNow },
                new Permission { Id = UserUpdatePermissionId, Code = "user.update", Name = "Update User", Type = PermissionType.Api, Description = "Update tenant user profile.", HttpMethod = "PUT", Route = "/api/identity/users/{userId}", CreatedAt = utcNow },
                new Permission { Id = UserAssignRolesPermissionId, Code = "user.assign_roles", Name = "Assign User Roles", Type = PermissionType.Api, Description = "Assign roles to a tenant user.", HttpMethod = "PUT", Route = "/api/identity/users/{userId}/roles", CreatedAt = utcNow },
                new Permission { Id = RoleViewPermissionId, Code = "role.view", Name = "View Roles", Type = PermissionType.Api, Description = "List tenant roles.", HttpMethod = "GET", Route = "/api/authorization/roles", CreatedAt = utcNow },
                new Permission { Id = RoleCreatePermissionId, Code = "role.create", Name = "Create Role", Type = PermissionType.Api, Description = "Create tenant role.", HttpMethod = "POST", Route = "/api/authorization/roles", CreatedAt = utcNow },
                new Permission { Id = RoleAssignPermissionId, Code = "role.assign_permissions", Name = "Assign Role Permissions", Type = PermissionType.Api, Description = "Grant permissions to a role.", HttpMethod = "PUT", Route = "/api/authorization/roles/{roleId}/permissions", CreatedAt = utcNow },
                new Permission { Id = PermissionViewPermissionId, Code = "permission.view", Name = "View Permissions", Type = PermissionType.Api, Description = "View system permissions.", HttpMethod = "GET", Route = "/api/authorization/permissions", CreatedAt = utcNow },
                new Permission { Id = MenuViewPermissionId, Code = "menu.view", Name = "View Menus", Type = PermissionType.Menu, Description = "View current user menus.", HttpMethod = "GET", Route = "/api/authorization/menus/current", CreatedAt = utcNow }
            };

            var existingPermissionCodes = await _dbContext.Permissions
                .AsNoTracking()
                .Select(x => x.Code)
                .ToListAsync(cancellationToken);

            var newPermissions = permissions
                .Where(permission => !existingPermissionCodes.Contains(permission.Code))
                .ToList();

            if (newPermissions.Count > 0)
            {
                await _dbContext.Permissions.AddRangeAsync(newPermissions, cancellationToken);
            }

            var menus = new[]
            {
                new Menu { Id = TenantRootMenuId, ParentId = null, Code = "tenant-center", Name = "Tenant Center", Path = "/tenants/current", Component = "tenancy/current", Icon = "building", Sort = 10, PermissionCode = "tenant.view", CreatedAt = utcNow },
                new Menu { Id = UserRootMenuId, ParentId = null, Code = "user-center", Name = "User Center", Path = "/users", Component = "identity/users", Icon = "users", Sort = 20, PermissionCode = "user.view", CreatedAt = utcNow },
                new Menu { Id = RoleRootMenuId, ParentId = null, Code = "role-center", Name = "Role Center", Path = "/roles", Component = "authorization/roles", Icon = "shield", Sort = 30, PermissionCode = "role.view", CreatedAt = utcNow },
                new Menu { Id = PermissionRootMenuId, ParentId = null, Code = "permission-center", Name = "Permission Center", Path = "/permissions", Component = "authorization/permissions", Icon = "key", Sort = 40, PermissionCode = "permission.view", CreatedAt = utcNow }
            };

            var existingMenuCodes = await _dbContext.Menus
                .AsNoTracking()
                .Select(x => x.Code)
                .ToListAsync(cancellationToken);

            var newMenus = menus
                .Where(menu => !existingMenuCodes.Contains(menu.Code))
                .ToList();

            if (newMenus.Count > 0)
            {
                await _dbContext.Menus.AddRangeAsync(newMenus, cancellationToken);
            }

            if (newPermissions.Count > 0 || newMenus.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
