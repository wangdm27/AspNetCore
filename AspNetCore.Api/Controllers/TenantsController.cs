using AspNetCore.Api.Infrastructure.Extensions;
using AspNetCore.Api.Modules.Authorization;
using AspNetCore.Api.Modules.Tenancy.Contracts;
using AspNetCore.Api.Modules.Tenancy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Route("api/tenancy/tenants")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public TenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<TenantResponse>> CreateAsync(
            [FromBody] CreateTenantRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _tenantService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetCurrentAsync), new { }, response);
        }

        [HttpGet("current")]
        [Authorize]
        [PermissionAuthorize("tenant.view")]
        public async Task<ActionResult<TenantResponse>> GetCurrentAsync(CancellationToken cancellationToken)
        {
            var response = await _tenantService.GetByIdAsync(HttpContext.GetRequiredTenantId(), cancellationToken);
            return Ok(response);
        }

        [HttpPost("current/users")]
        [Authorize]
        [PermissionAuthorize("tenant.user.add")]
        public async Task<ActionResult> AddUserAsync(
            [FromBody] AddTenantUserRequest request,
            CancellationToken cancellationToken)
        {
            await _tenantService.AddUserAsync(HttpContext.GetRequiredTenantId(), request, cancellationToken);
            return NoContent();
        }
    }
}
