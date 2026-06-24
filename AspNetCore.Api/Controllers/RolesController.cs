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

        [HttpGet("{roleId:guid}")]
        [PermissionAuthorize("role.view")]
        public async Task<ActionResult<RoleResponse>> GetByIdAsync(Guid roleId, CancellationToken cancellationToken)
        {
            // 复用 GetRolesAsync 然后筛选，或直接获取——当前用列表接口满足
            var roles = await _roleService.GetRolesAsync(HttpContext.GetRequiredTenantId(), cancellationToken);
            var role = roles.FirstOrDefault(x => x.RoleId == roleId)
                ?? throw new InvalidOperationException("Role does not exist.");
            return Ok(role);
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

        [HttpPut("{roleId:guid}")]
        [PermissionAuthorize("role.update")]
        public async Task<ActionResult<RoleResponse>> UpdateAsync(
            Guid roleId,
            [FromBody] UpdateRoleRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _roleService.UpdateAsync(HttpContext.GetRequiredTenantId(), roleId, request, cancellationToken);
            return Ok(response);
        }

        [HttpDelete("{roleId:guid}")]
        [PermissionAuthorize("role.delete")]
        public async Task<ActionResult> DeleteAsync(Guid roleId, CancellationToken cancellationToken)
        {
            await _roleService.DeleteAsync(HttpContext.GetRequiredTenantId(), roleId, cancellationToken);
            return NoContent();
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
