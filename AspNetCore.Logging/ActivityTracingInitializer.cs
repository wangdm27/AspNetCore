using System.Diagnostics;

namespace AspNetCore.Logging;

/// <summary>
/// 注册全局 <see cref="ActivityListener"/>，使自定义 ActivitySource（如 AspNetCore.RabbitMq）的 StartActivity 创建真实 Activity
/// </summary>
/// <remarks>
/// .NET ActivitySource 默认无监听器时 StartActivity 返回 null（零开销设计：无消费者则不创建）。
/// 日志库的 <see cref="Enrichers.ActivityTraceIdEnricher"/> 依赖 <see cref="Activity.Current"/> 有值，
/// 故需启用监听。否则消费端（RabbitMqConsumerBase 用自定义 ActivitySource 恢复链路）Activity 不创建，TraceId 缺失。
/// 静态字段持有 listener 防 GC 回收；<see cref="Interlocked"/> 保证多宿主/多次调用只注册一次。
/// </remarks>
internal static class ActivityTracingInitializer
{
    private static ActivityListener? _listener;
    private static int _registered;

    /// <summary>
    /// 启用全局 Activity 监听（幂等，多次调用只注册一次）
    /// </summary>
    public static void Enable()
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
        {
            return;
        }

        _listener = new ActivityListener
        {
            // 监听所有 ActivitySource：含 AspNetCore.RabbitMq（消费端链路恢复）及 ASP.NET Core 请求
            ShouldListenTo = _ => true,
            // 全数据采样，确保 Activity 创建且 TraceId/Baggage 完整
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
    }
}
