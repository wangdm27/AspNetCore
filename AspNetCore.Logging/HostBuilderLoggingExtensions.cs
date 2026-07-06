using AspNetCore.Logging.Enrichers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AspNetCore.Logging;

/// <summary>
/// 宿主日志接入扩展，三宿主（Api/Scheduler/EventDriven）统一接入 Serilog
/// </summary>
public static class HostBuilderLoggingExtensions
{
    /// <summary>
    /// 为 <see cref="IHostApplicationBuilder"/>（含 Api 的 WebApplicationBuilder）接入日志库
    /// </summary>
    /// <param name="builder">宿主应用构建器</param>
    /// <param name="configure">选项配置委托，可选；不传则从配置节 LoggingLib 绑定</param>
    /// <returns>宿主应用构建器</returns>
    public static IHostApplicationBuilder UseAspNetCoreLogging(
        this IHostApplicationBuilder builder,
        Action<LoggingOptions>? configure = null)
    {
        var options = BuildOptions(builder.Configuration, configure);
        var logger = CreateLogger(options);

        Log.Logger = logger;
        builder.Logging.AddSerilog(logger, dispose: true);

        RegisterEnricherInitializer(builder.Services, options);
        return builder;
    }

    /// <summary>
    /// 为 <see cref="IHostBuilder"/>（Scheduler/EventDriven Worker 宿主）接入日志库
    /// </summary>
    /// <param name="hostBuilder">宿主构建器</param>
    /// <param name="configure">选项配置委托，可选；不传则从配置节 LoggingLib 绑定</param>
    /// <returns>宿主构建器</returns>
    /// <remarks>
    /// 在 <see cref="IHostBuilder.ConfigureServices"/> 回调内从 ctx.Configuration 读 LoggingLib 节创建 Logger，
    /// 赋给全局 <see cref="Log.Logger"/>；<see cref="Serilog.Hosting.HostBuilderSerilogExtensions.UseSerilog"/>
    /// 不传 logger 时用全局 <see cref="Log.Logger"/>。
    /// </remarks>
    public static IHostBuilder UseAspNetCoreLogging(
        this IHostBuilder hostBuilder,
        Action<LoggingOptions>? configure = null)
    {
        hostBuilder.ConfigureServices((ctx, services) =>
        {
            var options = BuildOptions(ctx.Configuration, configure);
            var logger = CreateLogger(options);
            Log.Logger = logger;
            RegisterEnricherInitializer(services, options);
        });

        return hostBuilder.UseSerilog(dispose: true);
    }

    private static LoggingOptions BuildOptions(
        IConfiguration configuration,
        Action<LoggingOptions>? configure)
    {
        var options = new LoggingOptions();
        configuration.GetSection("LoggingLib").Bind(options);
        configure?.Invoke(options);
        return options;
    }

    private static Serilog.Core.Logger CreateLogger(LoggingOptions options)
    {
        // 启用全局 Activity 监听：使自定义 ActivitySource（如 AspNetCore.RabbitMq）的 StartActivity 创建真实 Activity，
        // 否则 ActivityTraceIdEnricher 读 Activity.Current 为 null，TraceId 缺失
        if (options.Enrichment.EnableTraceId)
        {
            ActivityTracingInitializer.Enable();
        }

        var loggerConfig = new LoggerConfiguration().ConfigureAspNetCoreLogging(options);
        return loggerConfig.CreateLogger();
    }

    private static void RegisterEnricherInitializer(IServiceCollection services, LoggingOptions options)
    {
        if (!options.Enrichment.EnableUserContext)
        {
            return;
        }

        // 启动服务在 host 起来后从 DI 取 IUserContextProvider 绑定到 enricher 静态 holder。
        // Web 宿主由接入端注册 IUserContextProvider 实现（包装 IHttpContextAccessor）；
        // Worker 宿主未注册，GetService 返回 null，enricher 跳过用户上下文。
        services.AddHostedService<HttpContextEnricherInitializer>();
    }
}

/// <summary>
/// 启动时绑定 <see cref="IUserContextProvider"/> 到 <see cref="HttpContextUserEnricher"/> 静态 holder
/// </summary>
/// <remarks>
/// Logger 在 DI 之前创建，enricher 无法构造注入；用 IHostedService 在 host 启动后延迟绑定。
/// Web 宿主：拿到 provider，UserId/TenantId 生效。Worker 宿主：provider 为 null，enricher 跳过。
/// </remarks>
internal sealed class HttpContextEnricherInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public HttpContextEnricherInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        HttpContextUserEnricher.Provider = _serviceProvider.GetService<IUserContextProvider>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
