using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AspNetCore.Api.Infrastructure.Auth
{
    /// <summary>
    /// JWT Token 服务实现类
    /// </summary>
    public sealed class JwtTokenService : ITokenService
    {
        /// <summary>
        /// JWT 配置选项
        /// </summary>
        private readonly JwtOptions _options;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">JWT 配置选项</param>
        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// 创建 JWT 访问令牌
        /// </summary>
        /// <param name="user">用户实体</param>
        /// <param name="tenant">租户实体</param>
        /// <param name="roleCodes">角色代码集合</param>
        /// <param name="permissionCodes">权限代码集合</param>
        /// <returns>Token 结果，包含访问令牌字符串和过期时间</returns>
        public TokenResult CreateToken(
            User user,
            Tenant tenant,
            IReadOnlyCollection<string> roleCodes,
            IReadOnlyCollection<string> permissionCodes)
        {
            // 获取当前 UTC 时间作为 Token 的签发时间
            var now = DateTime.UtcNow;
            // 计算 Token 过期时间（基于配置的过期分钟数）
            var expiresAt = now.AddMinutes(_options.AccessTokenExpiresMinutes);

            // 构建标准 Claim 列表
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),     // 主题（用户ID）
                new(JwtRegisteredClaimNames.UniqueName, user.UserName),   // 唯一用户名
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),       // 名称标识符（用户ID）
                new(ClaimTypes.Name, user.UserName),                      // 用户名
                new("display_name", user.DisplayName),                    // 显示名称（自定义Claim）
                new("tenant_id", tenant.Id.ToString()),                   // 租户ID（自定义Claim）
                new("tenant_code", tenant.Code)                           // 租户代码（自定义Claim）
            };

            // 添加角色 Claim（使用 ClaimTypes.Role 标准类型）
            claims.AddRange(roleCodes.Select(roleCode => new Claim(ClaimTypes.Role, roleCode)));
            // 添加权限 Claim（使用自定义 "permission" 类型）
            claims.AddRange(permissionCodes.Select(permissionCode => new Claim("permission", permissionCode)));

            // 创建对称安全密钥（使用配置的签名密钥）
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            // 创建签名凭据（使用 HMAC-SHA256 算法）
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 创建 JWT Token 对象
            var token = new JwtSecurityToken(
                issuer: _options.Issuer,          // 签发者
                audience: _options.Audience,      // 受众
                claims: claims,                   // Claim 列表
                notBefore: now,                   // 生效时间
                expires: expiresAt,               // 过期时间
                signingCredentials: credentials); // 签名凭据

            // 生成最终的 TokenResult
            return new TokenResult
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token), // 将 Token 对象序列化为字符串
                ExpiresAt = expiresAt                                          // Token 过期时间
            };
        }
    }
}