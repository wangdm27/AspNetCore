using AspNetCore.EventDriven.Infrastructure.Extensions;

namespace AspNetCore.EventDriven;

public class Program
{
    public static async Task Main(string[] args)
    {
        // 用 Host.CreateDefaultBuilder（返回 IHostBuilder），对齐 Scheduler 风格。
        // .NET 10 ConfigureWebHostDefaults 仅接 IHostBuilder，不接 IHostApplicationBuilder。
        var hostBuilder = Host.CreateDefaultBuilder(args);

        // RabbitMQ 基建 + 事件总线 + 消费者（含 HostedService 包装）
        hostBuilder.AddEventDriven();

        var host = hostBuilder.Build();
        await host.RunAsync();
    }
}
