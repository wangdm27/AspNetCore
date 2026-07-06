using AspNetCore.Logging.Enrichers;
using Serilog;
using Serilog.Events;

namespace AspNetCore.Logging;

/// <summary>
/// 按 <see cref="LoggingOptions"/> 组装 <see cref="LoggerConfiguration"/>（sinks/enrichers/filter）
/// </summary>
public static class LoggerConfigurationExtensions
{
    /// <summary>
    /// 应用 <see cref="LoggingOptions"/> 配置到 <paramref name="loggerConfig"/>
    /// </summary>
    /// <param name="loggerConfig">Serilog 配置构建器</param>
    /// <param name="options">日志库选项</param>
    /// <returns>配置后的构建器</returns>
    public static LoggerConfiguration ConfigureAspNetCoreLogging(
        this LoggerConfiguration loggerConfig,
        LoggingOptions options)
    {
        loggerConfig.MinimumLevel.Is(options.MinimumLevel);

        // Enrichers
        if (options.Enrichment.EnableTraceId)
        {
            loggerConfig.Enrich.With<ActivityTraceIdEnricher>();
        }
        if (options.Enrichment.EnableUserContext)
        {
            loggerConfig.Enrich.With<HttpContextUserEnricher>();
        }
        if (options.Enrichment.EnableMachineName)
        {
            loggerConfig.Enrich.FromLogContext()
                .Enrich.WithMachineName();
        }
        if (options.Enrichment.EnableThreadId)
        {
            loggerConfig.Enrich.WithThreadId();
        }

        loggerConfig.Enrich.WithProperty("ApplicationName", options.ApplicationName);

        // Sinks
        if (options.Sinks.EnableConsole)
        {
            loggerConfig.WriteTo.Console();
        }
        if (options.Sinks.EnableFile)
        {
            var path = System.IO.Path.Combine(
                options.Sinks.FileBasePath,
                options.ApplicationName,
                "log.log");
            loggerConfig.WriteTo.File(
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.Sinks.FileRetainedFileCountLimit,
                fileSizeLimitBytes: options.Sinks.FileSizeLimitBytes,
                shared: true);
        }
        if (options.Sinks.EnableSeq)
        {
            loggerConfig.WriteTo.Seq(
                serverUrl: options.Sinks.SeqUrl,
                restrictedToMinimumLevel: LogEventLevel.Verbose);
        }

        return loggerConfig;
    }
}
