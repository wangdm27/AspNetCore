using FluentAssertions;

using AspNetCore.Events;

namespace AspNetCore.Events.Tests;

/// <summary>
/// EventBusNaming 命名约定计算器单元测试。纯静态逻辑，分支：UseTypeNameAsRoutingKey。
/// </summary>
public class EventBusNamingTests
{
    private sealed class TestEvent : IEvent { }

    private static EventBusOptions DefaultOpts() => new();

    [Fact]
    public void ForPublish_WithDefaults_ReturnsPrefixedExchangeAndTypeNameRoutingKey()
    {
        // Act
        var (exchange, routingKey) = EventBusNaming.ForPublish<TestEvent>(DefaultOpts());

        // Assert
        exchange.Should().Be("evt.TestEvent");
        routingKey.Should().Be("TestEvent");
    }

    [Fact]
    public void ForPublish_WithRoutingKeyDisabled_ReturnsEmptyRoutingKey()
    {
        // Arrange
        var opts = DefaultOpts();
        opts.UseTypeNameAsRoutingKey = false;

        // Act
        var (exchange, routingKey) = EventBusNaming.ForPublish<TestEvent>(opts);

        // Assert
        exchange.Should().Be("evt.TestEvent");
        routingKey.Should().BeEmpty();
    }

    [Fact]
    public void ForConsume_WithDefaults_ReturnsPrefixedExchangeRoutingKeyAndQueue()
    {
        // Act
        var (exchange, routingKey, queue) = EventBusNaming.ForConsume<TestEvent>(DefaultOpts());

        // Assert
        exchange.Should().Be("evt.TestEvent");
        routingKey.Should().Be("TestEvent");
        queue.Should().Be("q.TestEvent");
    }

    [Fact]
    public void ForConsume_WithRoutingKeyDisabled_ReturnsEmptyRoutingKeyAndUnchangedQueue()
    {
        // Arrange
        var opts = DefaultOpts();
        opts.UseTypeNameAsRoutingKey = false;

        // Act
        var (exchange, routingKey, queue) = EventBusNaming.ForConsume<TestEvent>(opts);

        // Assert
        exchange.Should().Be("evt.TestEvent");
        routingKey.Should().BeEmpty();
        queue.Should().Be("q.TestEvent");
    }

    [Fact]
    public void ForConsume_WithCustomPrefixes_ReturnsCustomNames()
    {
        // Arrange
        var opts = new EventBusOptions { ExchangePrefix = "x.", QueuePrefix = "qq." };

        // Act
        var (exchange, routingKey, queue) = EventBusNaming.ForConsume<TestEvent>(opts);

        // Assert
        exchange.Should().Be("x.TestEvent");
        routingKey.Should().Be("TestEvent");
        queue.Should().Be("qq.TestEvent");
    }

    [Fact]
    public void ForPublish_AndForConsume_ReturnSameExchangeAndRoutingKey()
    {
        // Arrange
        var opts = DefaultOpts();

        // Act
        var pub = EventBusNaming.ForPublish<TestEvent>(opts);
        var con = EventBusNaming.ForConsume<TestEvent>(opts);

        // Assert — 发布端与消费端 exchange/routingKey 必须一致，消息才能正确路由
        pub.exchange.Should().Be(con.exchange);
        pub.routingKey.Should().Be(con.routingKey);
    }
}
