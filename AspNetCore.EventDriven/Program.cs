using AspNetCore.EventDriven.Infrastructure.Extensions;
using AspNetCore.Logging;

namespace AspNetCore.EventDriven;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 用 Host.CreateDefaultBuilder（返回 IHostBuilder），对齐 Scheduler 风格。
        // .NET 10 ConfigureWebHostDefaults 仅接 IHostBuilder，不接 IHostApplicationBuilder。
        var hostBuilder = Host.CreateDefaultBuilder(args);

        // Serilog 日志库：Console + File + Seq，TraceId 经 RabbitMq traceparent 头与 Api 发布端贯通
        hostBuilder.UseAspNetCoreLogging();

        // RabbitMQ 基建 + 事件总线 + 消费者（含 HostedService 包装）
        hostBuilder.AddEventDriven();

        var host = hostBuilder.Build();
        await host.RunAsync();
    }
}
