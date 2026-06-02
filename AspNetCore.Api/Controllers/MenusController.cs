using AspNetCore.Api.Infrastructure.Extensions;
using AspNetCore.Api.Modules.Authorization;
using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.Api.Modules.Authorization.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/authorization/menus")]
    public class MenusController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public MenusController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet("current")]
        [PermissionAuthorize("menu.view")]
        public async Task<ActionResult<IReadOnlyList<MenuResponse>>> GetCurrentAsync(CancellationToken cancellationToken)
        {
            var response = await _permissionService.GetCurrentMenusAsync(
                HttpContext.GetRequiredTenantId(),
                HttpContext.GetRequiredUserId(),
                cancellationToken);

            return Ok(response);
        }

        [HttpGet("current/routes")]
        [PermissionAuthorize("menu.view")]
        public async Task<ActionResult<IReadOnlyList<MenuResponse>>> GetCurrentRoutesAsync(CancellationToken cancellationToken)
        {
            var response = await _permissionService.GetCurrentRoutesAsync(
                HttpContext.GetRequiredTenantId(),
                HttpContext.GetRequiredUserId(),
                cancellationToken);

            return Ok(response);
        }
    }
}
