using AspNetCore.Api.Infrastructure.Context;
using AspNetCore.Api.Modules.Authorization.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AspNetCore.Api.Modules.Authorization
{
    public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permissionCode;
        private readonly ICurrentRequestContext _currentRequestContext;
        private readonly IPermissionChecker _permissionChecker;

        public PermissionAuthorizationFilter(
            string permissionCode,
            ICurrentRequestContext currentRequestContext,
            IPermissionChecker permissionChecker)
        {
            _permissionCode = permissionCode;
            _currentRequestContext = currentRequestContext;
            _permissionChecker = permissionChecker;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!_currentRequestContext.IsAuthenticated
                || !_currentRequestContext.UserId.HasValue
                || !_currentRequestContext.TenantId.HasValue)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var hasPermission = await _permissionChecker.HasPermissionAsync(
                _currentRequestContext.TenantId.Value,
                _currentRequestContext.UserId.Value,
                _permissionCode,
                context.HttpContext.RequestAborted);

            if (!hasPermission)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
