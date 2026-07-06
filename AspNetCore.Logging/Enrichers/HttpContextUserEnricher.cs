using Serilog.Core;
using Serilog.Events;

namespace AspNetCore.Logging.Enrichers;

/// <summary>
/// 将当前用户上下文（UserId/TenantId）附加到日志事件
/// </summary>
/// <remarks>
/// 零 ASP.NET Core 框架依赖：实际读取由 <see cref="IUserContextProvider"/> 实现承担
/// （Web 宿主包装 IHttpContextAccessor）。Logger 在 DI 之前创建，enricher 无法构造注入，
/// 故用静态 holder 由接入端启动服务延迟绑定。未绑定时 enricher 跳过（Worker 宿主）。
/// </remarks>
public sealed class HttpContextUserEnricher : ILogEventEnricher
{
    /// <summary>
    /// 用户上下文提供者静态 holder，由接入端启动服务绑定
    /// </summary>
    internal static IUserContextProvider? Provider;

    /// <summary>
    /// 附加 UserId / TenantId 属性到日志事件
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var provider = Provider;
        if (provider is null)
        {
            return;
        }

        var userId = provider.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
        }

        var tenantId = provider.TenantId;
        if (!string.IsNullOrEmpty(tenantId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
        }
    }
}
