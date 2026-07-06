using AspNetCore.Events;
using AspNetCore.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.EventDriven;

/// <summary>
/// 事件驱动消费者基类。继承 <see cref="RabbitMqConsumerBase{TEvent}"/>，
/// 按 <see cref="EventBusOptions"/> 约定自动 override Queue/Exchange/RoutingKey，
/// 子类只需实现 <see cref="RabbitMqConsumerBase{TEvent}.HandleAsync"/>，无需手写任何 AMQP 拓扑名。
/// </summary>
/// <typeparam name="TEvent">事件类型，须为引用类型并实现 <see cref="IEvent"/>。</typeparam>
public abstract class EventDrivenConsumerBase<TEvent> : RabbitMqConsumerBase<TEvent>
    where TEvent : class, IEvent
{
    private readonly EventBusOptions _opts;

    protected EventDrivenConsumerBase(
        [FromKeyedServices("consumer")] IRabbitMqChannelPool channelPool,
        RabbitMqOptions rabbitOpts,
        EventBusOptions eventOpts)
        : base(channelPool, rabbitOpts)
    {
        _opts = eventOpts;
    }

    protected override string Queue
    {
        get
        {
            var (_, _, q) = EventBusNaming.ForConsume<TEvent>(_opts);
            return q;
        }
    }

    protected override string Exchange
    {
        get
        {
            var (ex, _, _) = EventBusNaming.ForConsume<TEvent>(_opts);
            return ex;
        }
    }

    protected override string RoutingKey
    {
        get
        {
            var (_, rk, _) = EventBusNaming.ForConsume<TEvent>(_opts);
            return rk;
        }
    }

    protected override string ExchangeType => _opts.ExchangeType;
}
