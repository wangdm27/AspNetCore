using FluentAssertions;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqOutboxMessage POCO 单元测试：默认值、init 属性、set 属性。
/// </summary>
public class RabbitMqOutboxMessageTests
{
    [Fact]
    public void Defaults_NewInstance_HaveExpectedValues()
    {
        // Act
        var msg = new RabbitMqOutboxMessage
        {
            Exchange = "ex",
            RoutingKey = "rk",
            Body = new byte[] { 1, 2 }
        };

        // Assert — init 默认值
        msg.Id.Should().NotBeEmpty();
        msg.Headers.Should().NotBeNull().And.BeEmpty();
        msg.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        msg.PublishedAt.Should().BeNull();
        msg.RetryCount.Should().Be(0);
        msg.LastError.Should().BeNull();
        msg.NextAttemptAt.Should().BeNull();
        msg.DeadLettered.Should().BeFalse();
    }

    [Fact]
    public void SetMutableProperties_AssignedValues_RoundTripPreserved()
    {
        // Arrange
        var msg = new RabbitMqOutboxMessage
        {
            Exchange = "ex",
            RoutingKey = "rk",
            Body = Array.Empty<byte>()
        };
        var now = DateTimeOffset.UtcNow;

        // Act — set 属性（非 init）
        msg.PublishedAt = now;
        msg.RetryCount = 3;
        msg.LastError = "timeout";
        msg.NextAttemptAt = now.AddSeconds(10);
        msg.DeadLettered = true;

        // Assert
        msg.PublishedAt.Should().Be(now);
        msg.RetryCount.Should().Be(3);
        msg.LastError.Should().Be("timeout");
        msg.NextAttemptAt.Should().Be(now.AddSeconds(10));
        msg.DeadLettered.Should().BeTrue();
    }

    [Fact]
    public void Id_DefaultIsUniqueAcrossInstances()
    {
        // Act
        var a = new RabbitMqOutboxMessage { Exchange = "e", RoutingKey = "r", Body = Array.Empty<byte>() };
        var b = new RabbitMqOutboxMessage { Exchange = "e", RoutingKey = "r", Body = Array.Empty<byte>() };

        // Assert — 自动生成 GUID 唯一
        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void InitProperties_CanBeCustomized()
    {
        // Arrange
        var id = Guid.NewGuid();
        var headers = new Dictionary<string, object?> { ["k"] = "v" };
        var body = new byte[] { 9 };
        var created = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var msg = new RabbitMqOutboxMessage
        {
            Id = id,
            Exchange = "amq.direct",
            RoutingKey = "key",
            Body = body,
            Headers = headers,
            CreatedAt = created
        };

        // Assert
        msg.Id.Should().Be(id);
        msg.Exchange.Should().Be("amq.direct");
        msg.RoutingKey.Should().Be("key");
        msg.Body.Should().BeSameAs(body);
        msg.Headers.Should().BeSameAs(headers);
        msg.CreatedAt.Should().Be(created);
    }
}
