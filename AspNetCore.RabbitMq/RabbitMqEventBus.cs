using AspNetCore.Events;

namespace AspNetCore.RabbitMq;

/// <summary>
/// <see cref="IEventBus"/> 的 RabbitMQ 实现。屏蔽 AMQP 细节，按 <see cref="EventBusOptions"/> 约定算 exchange/routingKey。
/// 发布端通过 DI 取 <see cref="IEventBus"/>，无需感知 RabbitMQ。
/// </summary>
internal sealed class RabbitMqEventBus : IEventBus
{
    private readonly IRabbitMqPublisher _publisher;
    private readonly IRabbitMqOutbox _outbox;
    private readonly EventBusOptions _opts;

    public RabbitMqEventBus(IRabbitMqPublisher publisher, IRabbitMqOutbox outbox, EventBusOptions opts)
    {
        _publisher = publisher;
        _outbox = outbox;
        _opts = opts;
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(_opts);
        return _publisher.PublishAsync(exchange, routingKey, @event, confirm: true, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        var (exchange, routingKey) = EventBusNaming.ForPublish<TEvent>(_opts);
        return _outbox.EnqueueAsync(exchange, routingKey, @event, cancellationToken: cancellationToken);
    }
}
