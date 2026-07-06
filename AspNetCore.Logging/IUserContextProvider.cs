namespace AspNetCore.Logging;

/// <summary>
/// 当前用户上下文提供者抽象（UserId/TenantId）
/// </summary>
/// <remarks>
/// 日志库零 ASP.NET Core 框架依赖：Web 宿主在接入端实现此接口包装 IHttpContextAccessor；
/// Worker 宿主不注册，enricher 跳过用户上下文。解耦 Logger 与 HttpContext 类型。
/// </remarks>
public interface IUserContextProvider
{
    /// <summary>当前用户 ID，无则 null</summary>
    string? UserId { get; }

    /// <summary>当前租户 ID，无则 null</summary>
    string? TenantId { get; }
}
