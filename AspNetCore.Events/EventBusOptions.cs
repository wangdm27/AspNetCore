namespace AspNetCore.Events;

/// <summary>
/// 事件总线命名约定配置。按事件类型名自动算 exchange/routingKey/queue。
/// 发布端与消费端须使用相同的 <see cref="EventBusOptions"/>（各自从配置读，配置节必须一致）。
/// </summary>
public class EventBusOptions
{
    /// <summary>交换机名前缀。最终 exchange = <see cref="ExchangePrefix"/> + typeof(TEvent).Name。默认 "evt."。</summary>
    public string ExchangePrefix { get; set; } = "evt.";

    /// <summary>队列名前缀。最终 queue = <see cref="QueuePrefix"/> + typeof(TEvent).Name。默认 "q."。</summary>
    public string QueuePrefix { get; set; } = "q.";

    /// <summary>路由键是否使用事件类型名。routingKey = typeof(TEvent).Name。默认 true。</summary>
    public bool UseTypeNameAsRoutingKey { get; set; } = true;

    /// <summary>交换机类型，默认 "direct"。可改 "topic" 以支持通配符路由。</summary>
    public string ExchangeType { get; set; } = "direct";
}
