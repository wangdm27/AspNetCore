using AspNetCore.Api.Modules.Identity.Models;
using AspNetCore.DataAccess.Entities;

namespace AspNetCore.Api.Infrastructure.Auth
{
    public interface ITokenService
    {
        TokenResult CreateToken(User user, Tenant tenant, IReadOnlyCollection<string> roleCodes, IReadOnlyCollection<string> permissionCodes);
    }
}
