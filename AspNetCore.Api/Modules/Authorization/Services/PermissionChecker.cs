using AspNetCore.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Modules.Authorization.Services
{
    public sealed class PermissionChecker : IPermissionChecker
    {
        private readonly ApplicationDbContext _dbContext;

        public PermissionChecker(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionCode, CancellationToken cancellationToken)
        {
            return await (from userRole in _dbContext.UserRoles.AsNoTracking()
                          join rolePermission in _dbContext.RolePermissions.AsNoTracking()
                              on userRole.RoleId equals rolePermission.RoleId
                          join permission in _dbContext.Permissions.AsNoTracking()
                              on rolePermission.PermissionId equals permission.Id
                          where userRole.TenantId == tenantId
                                && userRole.UserId == userId
                                && permission.Code == permissionCode
                          select permission.Id)
                .AnyAsync(cancellationToken);
        }
    }
}
