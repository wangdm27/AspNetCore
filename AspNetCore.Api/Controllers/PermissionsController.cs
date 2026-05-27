using AspNetCore.Api.Modules.Authorization;
using AspNetCore.Api.Modules.Authorization.Contracts;
using AspNetCore.Api.Modules.Authorization.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/authorization/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionsController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpGet]
        [PermissionAuthorize("permission.view")]
        public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _permissionService.GetPermissionsAsync(cancellationToken);
            return Ok(response);
        }
    }
}
