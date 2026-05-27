using AspNetCore.Api.Infrastructure.Extensions;
using AspNetCore.Api.Modules.Authorization;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/identity/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [PermissionAuthorize("user.view")]
        public async Task<ActionResult<IReadOnlyList<UserListItemResponse>>> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _userService.GetTenantUsersAsync(HttpContext.GetRequiredTenantId(), cancellationToken);
            return Ok(response);
        }

        [HttpPut("{userId:guid}")]
        [PermissionAuthorize("user.update")]
        public async Task<ActionResult<UserProfileResponse>> UpdateAsync(
            Guid userId,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _userService.UpdateAsync(HttpContext.GetRequiredTenantId(), userId, request, cancellationToken);
            return Ok(response);
        }

        [HttpPut("{userId:guid}/roles")]
        [PermissionAuthorize("user.assign_roles")]
        public async Task<ActionResult> AssignRolesAsync(
            Guid userId,
            [FromBody] AssignUserRolesRequest request,
            CancellationToken cancellationToken)
        {
            await _userService.AssignRolesAsync(HttpContext.GetRequiredTenantId(), userId, request.RoleIds, cancellationToken);
            return NoContent();
        }
    }
}
