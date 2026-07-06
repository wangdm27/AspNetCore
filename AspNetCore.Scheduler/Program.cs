using AspNetCore.Logging;
using AspNetCore.Scheduler.Infrastructure;
using AspNetCore.Scheduler.Infrastructure.Extensions;

namespace AspNetCore.Scheduler;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 独立读配置 (与 host 同源: appsettings.json + 环境变量 + args)
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // build 前确保 Hangfire 库存在 (配置 AutoCreateDatabase=true 时)
        await HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        var hostBuilder = Host.CreateDefaultBuilder(args);

        // Serilog 日志库：Console + File + Seq，TraceId 贯穿（Scheduler 为独立进程，TraceId 限进程内）
        hostBuilder.UseAspNetCoreLogging();

        // 复合 host: 默认 builder (IHostBuilder) 嵌 Kestrel 暴露 Dashboard
        hostBuilder.ConfigureSchedulerWebHost();

        // Hangfire server + 存储 + Jobs 注册
        hostBuilder.AddSchedulerHangfire();

        var host = hostBuilder.Build();

        // 注册周期任务
        host.Services.UseSchedulerRecurringJobs();

        await host.RunAsync();
    }
}

