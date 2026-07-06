namespace AspNetCore.Logging;

/// <summary>
/// 日志 Enricher 配置
/// </summary>
public class LoggingEnrichmentOptions
{
    /// <summary>是否附加 TraceId/SpanId（取 <see cref="System.Diagnostics.Activity.Current"/>）</summary>
    /// <remarks>默认值: true。依赖 W3C TraceContext + RabbitMq 库 traceparent 透传实现跨进程链路贯通</remarks>
    public bool EnableTraceId { get; set; } = true;

    /// <summary>是否附加 UserId/TenantId（取 HttpContext Claims）</summary>
    /// <remarks>默认值: true。仅 Web 宿主生效；Worker 宿主无 HttpContext 自动跳过</remarks>
    public bool EnableUserContext { get; set; } = true;

    /// <summary>是否附加机器名</summary>
    /// <remarks>默认值: true</remarks>
    public bool EnableMachineName { get; set; } = true;

    /// <summary>是否附加线程 ID</summary>
    /// <remarks>默认值: true</remarks>
    public bool EnableThreadId { get; set; } = true;
}
