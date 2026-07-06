namespace AspNetCore.Events;

/// <summary>
/// 事件标记接口。所有事件 record 实现此接口，用于约束 <see cref="IEventBus"/> 泛型。
/// </summary>
public interface IEvent { }
