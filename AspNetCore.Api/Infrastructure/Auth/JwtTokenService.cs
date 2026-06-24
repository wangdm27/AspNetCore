using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AspNetCore.Api.Infrastructure.Auth
{
    /// <summary>
    /// JWT Token 服务实现类
    /// </summary>
    public sealed class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// 创建 JWT 访问令牌
        /// </summary>
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
                ExpiresAt = expiresAt,
                RefreshToken = GenerateRefreshToken()
            };
        }

        /// <summary>
        /// 生成 Refresh Token 原始值（加密随机字符串）
        /// </summary>
        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        /// <summary>
        /// 从过期的 Access Token 中提取 Claims（验证签名但忽略过期）
        /// </summary>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false, // 忽略过期
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    accessToken, tokenValidationParameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken
                    || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
