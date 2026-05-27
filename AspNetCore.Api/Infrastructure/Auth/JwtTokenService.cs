using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AspNetCore.Api.Infrastructure.Auth
{
    public sealed class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public TokenResult CreateToken(
            User user,
            Tenant tenant,
            IReadOnlyCollection<string> roleCodes,
            IReadOnlyCollection<string> permissionCodes)
        {
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(_options.AccessTokenExpiresMinutes);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName),
                new("display_name", user.DisplayName),
                new("tenant_id", tenant.Id.ToString()),
                new("tenant_code", tenant.Code)
            };

            claims.AddRange(roleCodes.Select(roleCode => new Claim(ClaimTypes.Role, roleCode)));
            claims.AddRange(permissionCodes.Select(permissionCode => new Claim("permission", permissionCode)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: credentials);

            return new TokenResult
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt
            };
        }
    }
}
