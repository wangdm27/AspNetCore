using AspNetCore.Api.Infrastructure.Extensions;
using AspNetCore.Api.Modules.Identity.Contracts;
using AspNetCore.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Route("api/identity/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> RegisterAsync(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> LoginAsync(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserProfileResponse>> GetCurrentAsync(CancellationToken cancellationToken)
        {
            var response = await _authService.GetCurrentUserProfileAsync(
                HttpContext.GetRequiredUserId(),
                HttpContext.GetRequiredTenantId(),
                cancellationToken);

            return Ok(response);
        }
    }
}
