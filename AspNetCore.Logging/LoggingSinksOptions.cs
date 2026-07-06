namespace AspNetCore.Logging;

/// <summary>
/// 日志 Sink 配置
/// </summary>
public class LoggingSinksOptions
{
    /// <summary>是否启用控制台输出（开发期实时查看）</summary>
    /// <remarks>默认值: true</remarks>
    public bool EnableConsole { get; set; } = true;

    /// <summary>是否启用文件输出（按日滚动，按应用分目录，兜底/归档）</summary>
    /// <remarks>默认值: true。路径: {FileBasePath}/{ApplicationName}/logyyyyMMdd.log</remarks>
    public bool EnableFile { get; set; } = true;

    /// <summary>是否启用 Seq 输出（主查询入口，结构化检索）</summary>
    /// <remarks>默认值: true。连 SeqUrl，未装 Seq 时 Serilog 容错重试不崩</remarks>
    public bool EnableSeq { get; set; } = true;

    /// <summary>Seq 服务地址</summary>
    /// <remarks>默认值: http://localhost:5341</remarks>
    public string SeqUrl { get; set; } = "http://localhost:5341";

    /// <summary>日志文件根目录</summary>
    /// <remarks>实际路径为 {FileBasePath}/{ApplicationName}/。默认值: logs</remarks>
    public string FileBasePath { get; set; } = "logs";

    /// <summary>保留日志文件数量（按日滚动，超出删除最旧；null 不限）</summary>
    /// <remarks>默认值: 14</remarks>
    public int? FileRetainedFileCountLimit { get; set; } = 14;

    /// <summary>单个日志文件大小上限（字节），超出滚动新文件；null 不限</summary>
    /// <remarks>默认值: 10MB</remarks>
    public long? FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;
}
