using AspNetCore.Events;
using AspNetCore.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EventDriven.Consumers;

/// <summary>
/// 示例消费者：处理 <see cref="UserCreatedEvent"/>，写日志。
/// 继承 <see cref="EventDrivenConsumerBase{TEvent}"/>，无需手写 Queue/Exchange/RoutingKey。
/// </summary>
public sealed class UserCreatedEventConsumer : EventDrivenConsumerBase<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedEventConsumer> _logger;

    public UserCreatedEventConsumer(
        [FromKeyedServices("consumer")] IRabbitMqChannelPool channelPool,
        RabbitMqOptions rabbitOpts,
        EventBusOptions eventOpts,
        ILogger<UserCreatedEventConsumer> logger)
        : base(channelPool, rabbitOpts, eventOpts)
    {
        _logger = logger;
    }

    protected override Task HandleAsync(UserCreatedEvent message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Received UserCreatedEvent: UserId={UserId} UserName={UserName} Email={Email} CreatedAt={CreatedAt}",
            message.UserId, message.UserName, message.Email, message.CreatedAt);
        return Task.CompletedTask;
    }
}
