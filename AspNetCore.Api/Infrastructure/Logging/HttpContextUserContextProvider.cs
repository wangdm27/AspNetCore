using System.Security.Claims;
using AspNetCore.Logging;
using Microsoft.AspNetCore.Http;

namespace AspNetCore.Api.Infrastructure.Logging;

/// <summary>
/// <see cref="IUserContextProvider"/> 实现：从 <see cref="IHttpContextAccessor"/> 读 Claims 的 UserId/TenantId
/// </summary>
/// <remarks>
/// 供 AspNetCore.Logging 的 <c>HttpContextUserEnricher</c> 在 host 启动后绑定（静态 holder）。
/// 与 Api 的 <c>CurrentRequestContext</c> 解耦：日志库独立读 Claims，避免相互依赖。
/// </remarks>
internal sealed class HttpContextUserContextProvider : IUserContextProvider
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextUserContextProvider(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <inheritdoc/>
    public string? UserId =>
        _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc/>
    public string? TenantId =>
        _accessor.HttpContext?.User?.FindFirstValue("tenant_id");
}
