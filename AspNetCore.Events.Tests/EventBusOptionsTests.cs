using FluentAssertions;

using AspNetCore.Events;

namespace AspNetCore.Events.Tests;

/// <summary>
/// EventBusOptions POCO 默认值与回读单元测试。
/// </summary>
public class EventBusOptionsTests
{
    [Fact]
    public void Defaults_NewInstance_HaveExpectedValues()
    {
        // Act
        var opts = new EventBusOptions();

        // Assert
        opts.ExchangePrefix.Should().Be("evt.");
        opts.QueuePrefix.Should().Be("q.");
        opts.UseTypeNameAsRoutingKey.Should().BeTrue();
        opts.ExchangeType.Should().Be("direct");
    }

    [Fact]
    public void SetProperties_AssignedValues_RoundTripPreserved()
    {
        // Act
        var opts = new EventBusOptions
        {
            ExchangePrefix = "x.",
            QueuePrefix = "queue.",
            UseTypeNameAsRoutingKey = false,
            ExchangeType = "topic"
        };

        // Assert
        opts.ExchangePrefix.Should().Be("x.");
        opts.QueuePrefix.Should().Be("queue.");
        opts.UseTypeNameAsRoutingKey.Should().BeFalse();
        opts.ExchangeType.Should().Be("topic");
    }

    [Fact]
    public void Defaults_AlignWithEventBusNamingExpectations()
    {
        // Arrange — 默认值与 EventBusNaming 计算结果的一致性契约
        var opts = new EventBusOptions();
        var (exchange, routingKey, queue) = EventBusNaming.ForConsume<UserCreatedEvent>(opts);

        // Assert — 默认前缀产生 evt./q. + 事件类型名
        exchange.Should().Be("evt." + nameof(UserCreatedEvent));
        queue.Should().Be("q." + nameof(UserCreatedEvent));
        routingKey.Should().Be(nameof(UserCreatedEvent));
    }
}
