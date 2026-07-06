using AspNetCore.EventDriven.Consumers;
using AspNetCore.Events;
using AspNetCore.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.EventDriven.Infrastructure.Extensions;

public static class EventDrivenServiceExtensions
{
    /// <summary>
    /// 注册 RabbitMQ 基建 + 事件总线约定 + 所有消费者（含 HostedService 包装）。
    /// 对齐 <c>Scheduler.AddSchedulerHangfire</c> 的 IHostBuilder 扩展风格。
    /// </summary>
    public static IHostBuilder AddEventDriven(this IHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
        {
            var cfg = ctx.Configuration;
            var rmq = cfg.GetSection("RabbitMq");

            // 1. RabbitMQ 基建（连接 + 双池 + publisher + outbox + dispatcher）
            services.AddUnifiedRabbitMq(opt =>
            {
                opt.HostName = rmq["HostName"] ?? "localhost";
                opt.Port = rmq.GetValue<int?>("Port") ?? 5672;
                opt.UserName = rmq["UserName"] ?? "guest";
                opt.Password = rmq["Password"] ?? "guest";
                opt.VirtualHost = rmq["VirtualHost"] ?? "/";
                opt.PrefetchCount = (ushort)(rmq.GetValue<int?>("PrefetchCount") ?? 10);
                opt.ChannelPoolSize = rmq.GetValue<int?>("ChannelPoolSize") ?? 16;
                opt.ConsumerChannelPoolSize = rmq.GetValue<int?>("ConsumerChannelPoolSize") ?? 16;
                opt.EnableDeadLetter = rmq.GetValue<bool?>("EnableDeadLetter") ?? false;
                opt.DeadLetterExchange = rmq["DeadLetterExchange"] ?? string.Empty;
                opt.DeadLetterRoutingKey = rmq["DeadLetterRoutingKey"] ?? string.Empty;
                opt.DeadLetterQueue = rmq["DeadLetterQueue"] ?? string.Empty;
                opt.MaxRetryCount = rmq.GetValue<int?>("MaxRetryCount") ?? 5;
                opt.RetryBaseDelay = TimeSpan.FromSeconds(rmq.GetValue<int?>("RetryBaseDelaySeconds") ?? 5);
                opt.RetryMaxDelay = TimeSpan.FromMinutes(rmq.GetValue<int?>("RetryMaxDelayMinutes") ?? 5);
            });

            // 2. 事件总线约定（从 EventBus 配置节读，默认 evt./q./direct）
            var evtCfg = cfg.GetSection("EventBus");
            services.AddRabbitMqEventBus(opt =>
            {
                opt.ExchangePrefix = evtCfg["ExchangePrefix"] ?? "evt.";
                opt.QueuePrefix = evtCfg["QueuePrefix"] ?? "q.";
                opt.ExchangeType = evtCfg["ExchangeType"] ?? "direct";
            });

            // 3. 注册消费者为 Singleton（长租通道，生命周期与 host 一致，不可 Scoped/Transient）
            services.AddSingleton<UserCreatedEventConsumer>();
            // 4. 每个消费者包一层 HostedService，随 host 自动启停
            services.AddHostedService<RabbitMqConsumerHostedService<UserCreatedEventConsumer>>();
        });
        return builder;
    }
}
