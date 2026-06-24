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

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> RefreshTokenAsync(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Ok(response);
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<ActionResult> ChangePasswordAsync(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            await _authService.ChangePasswordAsync(
                HttpContext.GetRequiredUserId(),
                HttpContext.GetRequiredTenantId(),
                request,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult> ForgotPasswordAsync(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            await _authService.ForgotPasswordAsync(request, cancellationToken);
            // 无论用户是否存在都返回 200，防止信息泄露
            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult> ResetPasswordAsync(
            [FromBody] ConfirmResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            await _authService.ResetPasswordAsync(request, cancellationToken);
            return Ok(new { message = "Password has been reset successfully." });
        }
    }
}