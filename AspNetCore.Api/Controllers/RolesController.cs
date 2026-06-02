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
    [Route("api/authorization/roles")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        [PermissionAuthorize("role.view")]
        public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _roleService.GetRolesAsync(HttpContext.GetRequiredTenantId(), cancellationToken);
            return Ok(response);
        }

        [HttpPost]
        [PermissionAuthorize("role.create")]
        public async Task<ActionResult<RoleResponse>> CreateAsync(
            [FromBody] CreateRoleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _roleService.CreateAsync(HttpContext.GetRequiredTenantId(), request, cancellationToken);
            return Ok(response);
        }


        [HttpGet("{roleId:guid}/permissions")]
        [PermissionAuthorize("role.view")]
        public async Task<ActionResult<RolePermissionSummaryResponse>> GetPermissionsAsync(
            Guid roleId,
            CancellationToken cancellationToken)
        {
            var response = await _roleService.GetRolePermissionsAsync(HttpContext.GetRequiredTenantId(), roleId, cancellationToken);
            return Ok(response);
        }

        [HttpPut("{roleId:guid}/permissions")]
        [PermissionAuthorize("role.assign_permissions")]
        public async Task<ActionResult> AssignPermissionsAsync(
            Guid roleId,
            [FromBody] AssignRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            await _roleService.AssignPermissionsAsync(HttpContext.GetRequiredTenantId(), roleId, request.PermissionIds, cancellationToken);
            return NoContent();
        }

        [HttpPut("{roleId:guid}/menus")]
        [PermissionAuthorize("role.assign_menus")]
        public async Task<ActionResult> AssignMenusAsync(
            Guid roleId,
            [FromBody] AssignRoleMenusRequest request,
            CancellationToken cancellationToken)
        {
            await _roleService.AssignMenusAsync(HttpContext.GetRequiredTenantId(), roleId, request, cancellationToken);
            return NoContent();
        }
    }
}
