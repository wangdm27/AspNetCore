using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;
using System.Security.Claims;

namespace AspNetCore.Api.Infrastructure.Auth
{
    /// <summary>
    /// Token 服务接口
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// 创建访问令牌
        /// </summary>
        TokenResult CreateToken(User user, Tenant tenant, IReadOnlyCollection<string> roleCodes, IReadOnlyCollection<string> permissionCodes);

        /// <summary>
        /// 生成 Refresh Token 原始值
        /// </summary>
        string GenerateRefreshToken();

        /// <summary>
        /// 从过期的 Access Token 中提取 Claims（验证签名但忽略过期）
        /// </summary>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
    }
}
