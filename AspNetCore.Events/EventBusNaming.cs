namespace AspNetCore.Events;

/// <summary>
/// 事件命名约定计算器。发布端与消费端共用，确保 exchange/routingKey/queue 一致。
/// </summary>
public static class EventBusNaming
{
    /// <summary>
    /// 计算发布端所需的 exchange 与 routingKey。
    /// </summary>
    public static (string exchange, string routingKey) ForPublish<TEvent>(EventBusOptions opts) where TEvent : IEvent
    {
        var name = typeof(TEvent).Name;
        var exchange = opts.ExchangePrefix + name;
        var routingKey = opts.UseTypeNameAsRoutingKey ? name : string.Empty;
        return (exchange, routingKey);
    }

    /// <summary>
    /// 计算消费端所需的 exchange、routingKey 与 queue。
    /// </summary>
    public static (string exchange, string routingKey, string queue) ForConsume<TEvent>(EventBusOptions opts) where TEvent : IEvent
    {
        var name = typeof(TEvent).Name;
        var exchange = opts.ExchangePrefix + name;
        var routingKey = opts.UseTypeNameAsRoutingKey ? name : string.Empty;
        var queue = opts.QueuePrefix + name;
        return (exchange, routingKey, queue);
    }
}
