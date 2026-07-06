using Serilog.Events;

namespace AspNetCore.Logging;

/// <summary>
/// 日志库顶层配置选项
/// </summary>
/// <remarks>
/// 对齐 <c>AspNetCore.Redis/RedisOptions</c> 风格：POCO + 默认值 + Action 绑定（不走 IOptions&lt;T&gt;）。
/// 三宿主（Api/Scheduler/EventDriven）各自实例化并从 appsettings LoggingLib 节填充。
/// </remarks>
public class LoggingOptions
{
    /// <summary>
    /// 应用名称
    /// </summary>
    /// <remarks>用于 File 目录分区与 Seq source 区分。默认值: app</remarks>
    public string ApplicationName { get; set; } = "app";

    /// <summary>
    /// 全局最低日志级别
    /// </summary>
    /// <remarks>默认值: Information</remarks>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Sink 配置（Console/File/Seq）
    /// </summary>
    public LoggingSinksOptions Sinks { get; set; } = new();

    /// <summary>
    /// Enricher 配置（TraceId/UserContext/MachineName/ThreadId）
    /// </summary>
    public LoggingEnrichmentOptions Enrichment { get; set; } = new();
}
