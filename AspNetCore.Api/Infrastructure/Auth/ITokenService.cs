using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;

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
        /// <param name="user">用户实体</param>
        /// <param name="tenant">租户实体</param>
        /// <param name="roleCodes">角色代码集合</param>
        /// <param name="permissionCodes">权限代码集合</param>
        /// <returns>Token 结果，包含访问令牌和过期时间</returns>
        TokenResult CreateToken(User user, Tenant tenant, IReadOnlyCollection<string> roleCodes, IReadOnlyCollection<string> permissionCodes);
    }
}