using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace AspNetCore.Logging.Enrichers;

/// <summary>
/// 将当前 <see cref="Activity"/> 的 TraceId/SpanId 附加到日志事件
/// </summary>
/// <remarks>
/// 依赖 .NET 内置 W3C TraceContext。<see cref="Activity.Current"/> 由 ASP.NET Core 请求自动创建，
/// 跨 RabbitMq 进程时由 AspNetCore.RabbitMq 库注入/提取 traceparent 头恢复延续。
/// 无 Activity 时输出空字符串。
/// </remarks>
public sealed class ActivityTraceIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// 附加 TraceId / SpanId 属性到日志事件
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToHexString() ?? string.Empty;
        var spanId = activity?.SpanId.ToHexString() ?? string.Empty;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", spanId));
    }
}
