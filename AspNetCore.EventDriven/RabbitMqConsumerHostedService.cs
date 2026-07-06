using AspNetCore.RabbitMq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EventDriven;

/// <summary>
/// 把 <see cref="IRabbitMqConsumer"/> 包装成 <see cref="IHostedService"/>，使消费者随 host 自动启停。
/// 解决 <see cref="RabbitMqConsumerBase{T}"/> 非 IHostedService 的缺口：
///   <see cref="StartAsync"/> → consumer.StartAsync
///   <see cref="StopAsync"/>  → consumer.DisposeAsync（先 BasicCancel 再归还通道租约，对重复释放幂等）
/// </summary>
public sealed class RabbitMqConsumerHostedService<TConsumer> : IHostedService, IAsyncDisposable
    where TConsumer : IRabbitMqConsumer, IAsyncDisposable
{
    private readonly TConsumer _consumer;
    private readonly ILogger<RabbitMqConsumerHostedService<TConsumer>> _logger;

    public RabbitMqConsumerHostedService(TConsumer consumer, ILogger<RabbitMqConsumerHostedService<TConsumer>> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting consumer {ConsumerType}", typeof(TConsumer).Name);
        await _consumer.StartAsync(cancellationToken);
        _logger.LogInformation("Consumer {ConsumerType} started", typeof(TConsumer).Name);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping consumer {ConsumerType}", typeof(TConsumer).Name);
        // RabbitMqConsumerBase 停止靠 DisposeAsync：先 BasicCancel 再归还通道租约。无 StopAsync 方法。
        await _consumer.DisposeAsync();
        _logger.LogInformation("Consumer {ConsumerType} stopped", typeof(TConsumer).Name);
    }

    public ValueTask DisposeAsync()
    {
        // host 停止时 StopAsync 已 DisposeAsync；此处兜底幂等（RabbitMqConsumerBase.DisposeAsync 内部判 _lease null 直接 return）。
        return _consumer.DisposeAsync();
    }
}
