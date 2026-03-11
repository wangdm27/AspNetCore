using AspNetCore.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static AspNetCore.Test3.DemoConsumer;

namespace AspNetCore.Test3
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddUnifiedRabbitMq(opt =>
            {
                opt.HostName = "localhost";
                opt.UserName = "guest";
                opt.Password = "guest";

                // demo: 连接恢复、Channel 池、Outbox 批处理参数
                opt.ChannelPoolSize = 8;
                opt.OutboxDispatchInterval = TimeSpan.FromSeconds(2);
                opt.OutboxBatchSize = 50;
            });

            builder.Services.AddSingleton<IRabbitMqConsumer, DemoConsumer>();

            await using var host = builder.Build();
            await host.StartAsync();

            var consumer = host.Services.GetRequiredService<IRabbitMqConsumer>();
            await consumer.StartAsync();

            // 1) 常规实时发布
            var publisher = host.Services.GetRequiredService<IRabbitMqPublisher>();
            await publisher.PublishAsync(
                exchange: "demo.exchange",
                routingKey: "demo.key",
                new DemoMessage { Text = "Hello RabbitMQ (direct publish)" });

            // 2) Outbox 入箱，后台调度器会异步投递
            var outbox = host.Services.GetRequiredService<IRabbitMqOutbox>();
            await outbox.EnqueueAsync(
                exchange: "demo.exchange",
                routingKey: "demo.key",
                new DemoMessage { Text = "Hello RabbitMQ (outbox message)" });

            Console.WriteLine("已发送 1 条直发消息 + 1 条 Outbox 消息，等待后台分发...");
            await Task.Delay(TimeSpan.FromSeconds(5));

            await host.StopAsync();
        }
    }
}
