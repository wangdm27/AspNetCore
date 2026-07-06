namespace AspNetCore.Events;

/// <summary>
/// 事件总线抽象。屏蔽底层 AMQP 细节，按 <see cref="EventBusOptions"/> 命名约定自动计算 exchange/routingKey。
/// 发布方（Api）仅依赖此接口，不感知 RabbitMQ。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 直发：立即投递到 broker（经发布确认 confirm）。
    /// 适用于实时性优先、允许失败时抛异常的场景。
    /// </summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;

    /// <summary>
    /// Outbox：先入发件箱，由后台调度器可靠投递（含重试退避 + 死信兜底）。
    /// 适用于可靠性优先、调用方不阻塞等待 broker 的场景。
    /// </summary>
    ValueTask EnqueueAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
}
