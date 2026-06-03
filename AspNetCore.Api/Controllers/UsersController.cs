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
        public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetAsync(
            CancellationToken cancellationToken,
            string? keyword = null,
            bool? isActive = null,
            int pageIndex = 1,
            int pageSize = 20)
        {
            var request = new UserQueryRequest
            {
                Keyword = keyword,
                IsActive = isActive,
                PageIndex = pageIndex,
                PageSize = pageSize
            };

            var response = await _userService.GetTenantUsersAsync(HttpContext.GetRequiredTenantId(), request, cancellationToken);
            return Ok(response);
        }

        [HttpGet("{userId:guid}")]
        [PermissionAuthorize("user.view")]
        public async Task<ActionResult<UserProfileResponse>> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
        {
            var response = await _userService.GetAsync(HttpContext.GetRequiredTenantId(), userId, cancellationToken);
            return Ok(response);
        }

        [HttpPost]
        [PermissionAuthorize("user.create")]
        public async Task<ActionResult<UserProfileResponse>> CreateAsync(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _userService.CreateAsync(HttpContext.GetRequiredTenantId(), request, cancellationToken);
            return Created($"/api/identity/users/{response.UserId}", response);
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

        [HttpDelete("{userId:guid}")]
        [PermissionAuthorize("user.delete")]
        public async Task<ActionResult> DeleteAsync(Guid userId, CancellationToken cancellationToken)
        {
            await _userService.DeleteAsync(
                HttpContext.GetRequiredTenantId(),
                userId,
                HttpContext.GetRequiredUserId(),
                cancellationToken);

            return NoContent();
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
