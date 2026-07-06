using System.Diagnostics;
using System.Text;

namespace AspNetCore.RabbitMq;

/// <summary>
/// RabbitMQ 跨进程链路追踪辅助：手动注入/提取 W3C traceparent 头
/// </summary>
/// <remarks>
/// W3C TraceContext 格式：<c>00-{traceId32hex}-{spanId16hex}-{flags2hex}</c>，ASP.NET Core 默认启用。
/// 发布端 <see cref="Inject"/> 把当前 Activity 的 traceparent 写入消息头；
/// 消费端 <see cref="ExtractAndStartActivity"/> 从消息头解析父 ActivityContext 并创建延续 Activity，
/// 使 <c>ILogger</c>（经 AspNetCore.Logging 的 ActivityTraceIdEnricher）输出的 TraceId 与发布端一致，
/// Seq 可按 TraceId 串联 Api→MQ→EventDriven 全链路。手动解析避开 DistributedContextPropagator 泛型推断，零 Logging 库依赖（纯 BCL）。
/// </remarks>
internal static class RabbitMqTracing
{
    private const string ActivitySourceName = "AspNetCore.RabbitMq";
    private const string ConsumeActivityName = "RabbitMq.Consume";
    private const string TraceParentHeader = "traceparent";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    /// <summary>
    /// 注入当前 Activity 的 W3C traceparent 到消息头
    /// </summary>
    /// <param name="headers">消息头字典，须非 null</param>
    /// <remarks>无当前 Activity 时直接返回，不修改头</remarks>
    public static void Inject(IDictionary<string, object?> headers)
    {
        var activity = Activity.Current;
        if (activity is null || activity.TraceId == default)
        {
            return;
        }

        // W3C traceparent: version(00)-traceId(32hex)-spanId(16hex)-flags(01=recorded)
        var traceparent = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-01";
        headers[TraceParentHeader] = traceparent;
    }

    /// <summary>
    /// 从消息头提取 traceparent 并创建 Consumer 延续 Activity
    /// </summary>
    /// <param name="headers">消息头字典，可能为 null</param>
    /// <returns>延续 Activity；无监听器时为 null，调用方用 using 释放</returns>
    public static Activity? ExtractAndStartActivity(IDictionary<string, object?>? headers)
    {
        if (TryGetTraceParent(headers, out var traceparent)
            && TryParseTraceParent(traceparent, out var parentContext))
        {
            return _activitySource.StartActivity(ConsumeActivityName, ActivityKind.Consumer, parentContext);
        }

        // 无父链路：仍创建 Activity（独立 TraceId），保持消费侧日志结构一致
        return _activitySource.StartActivity(ConsumeActivityName, ActivityKind.Consumer);
    }

    private static bool TryGetTraceParent(IDictionary<string, object?>? headers, out string traceparent)
    {
        traceparent = string.Empty;
        if (headers is null || !headers.TryGetValue(TraceParentHeader, out var raw) || raw is null)
        {
            return false;
        }

        // RabbitMQ 头值可能为 string 或 byte[]（UTF-8）
        traceparent = raw switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(traceparent);
    }

    private static bool TryParseTraceParent(string traceparent, out ActivityContext parentContext)
    {
        parentContext = default;
        var parts = traceparent.Split('-');
        if (parts.Length != 4 || parts[0] != "00")
        {
            return false;
        }

        try
        {
            var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
            var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
            // isRemote=true 标记为远程传入的父上下文，确保 TraceId 正确延续
            parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
