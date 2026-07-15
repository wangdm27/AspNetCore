using FluentAssertions;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqOptions 配置默认值与赋值回读单元测试。纯 POCO，无依赖。
/// </summary>
public class RabbitMqOptionsTests
{
    [Fact]
    public void Defaults_NewInstance_HaveExpectedValues()
    {
        // Act
        var opts = new RabbitMqOptions();

        // Assert — 连接
        opts.HostName.Should().Be("localhost");
        opts.Port.Should().Be(5672);
        opts.UserName.Should().Be("guest");
        opts.Password.Should().Be("guest");
        opts.VirtualHost.Should().Be("/");
        opts.PrefetchCount.Should().Be((ushort)10);
        opts.AutomaticRecoveryEnabled.Should().BeTrue();
        opts.TopologyRecoveryEnabled.Should().BeTrue();
        opts.NetworkRecoveryInterval.Should().Be(TimeSpan.FromSeconds(10));

        // 通道池
        opts.ChannelPoolSize.Should().Be(16);
        opts.ConsumerChannelPoolSize.Should().Be(16);

        // Outbox / 重试 / 确认
        opts.OutboxDispatchInterval.Should().Be(TimeSpan.FromSeconds(3));
        opts.OutboxBatchSize.Should().Be(100);
        opts.MaxRetryCount.Should().Be(5);
        opts.RetryBaseDelay.Should().Be(TimeSpan.FromSeconds(5));
        opts.RetryMaxDelay.Should().Be(TimeSpan.FromMinutes(5));
        opts.PublisherConfirmTimeout.Should().Be(TimeSpan.FromSeconds(10));

        // 死信
        opts.EnableDeadLetter.Should().BeFalse();
        opts.DeadLetterExchange.Should().BeEmpty();
        opts.DeadLetterRoutingKey.Should().BeEmpty();
        opts.DeadLetterQueue.Should().BeEmpty();

        // TTL
        opts.DefaultMessageTTL.Should().BeNull();
    }

    [Fact]
    public void SetProperties_AssignedValues_RoundTripPreserved()
    {
        // Arrange
        var opts = new RabbitMqOptions
        {
            HostName = "rabbit",
            Port = 5673,
            UserName = "admin",
            Password = "p@ss",
            VirtualHost = "/prod",
            PrefetchCount = 50,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(30),
            ChannelPoolSize = 32,
            ConsumerChannelPoolSize = 8,
            OutboxDispatchInterval = TimeSpan.FromSeconds(1),
            OutboxBatchSize = 25,
            MaxRetryCount = 10,
            RetryBaseDelay = TimeSpan.FromSeconds(2),
            RetryMaxDelay = TimeSpan.FromMinutes(1),
            PublisherConfirmTimeout = TimeSpan.FromSeconds(5),
            EnableDeadLetter = true,
            DeadLetterExchange = "dlx",
            DeadLetterRoutingKey = "dlk",
            DeadLetterQueue = "dlq",
            DefaultMessageTTL = TimeSpan.FromMinutes(2)
        };

        // Assert
        opts.HostName.Should().Be("rabbit");
        opts.Port.Should().Be(5673);
        opts.UserName.Should().Be("admin");
        opts.Password.Should().Be("p@ss");
        opts.VirtualHost.Should().Be("/prod");
        opts.PrefetchCount.Should().Be((ushort)50);
        opts.AutomaticRecoveryEnabled.Should().BeFalse();
        opts.TopologyRecoveryEnabled.Should().BeFalse();
        opts.NetworkRecoveryInterval.Should().Be(TimeSpan.FromSeconds(30));
        opts.ChannelPoolSize.Should().Be(32);
        opts.ConsumerChannelPoolSize.Should().Be(8);
        opts.OutboxDispatchInterval.Should().Be(TimeSpan.FromSeconds(1));
        opts.OutboxBatchSize.Should().Be(25);
        opts.MaxRetryCount.Should().Be(10);
        opts.RetryBaseDelay.Should().Be(TimeSpan.FromSeconds(2));
        opts.RetryMaxDelay.Should().Be(TimeSpan.FromMinutes(1));
        opts.PublisherConfirmTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.EnableDeadLetter.Should().BeTrue();
        opts.DeadLetterExchange.Should().Be("dlx");
        opts.DeadLetterRoutingKey.Should().Be("dlk");
        opts.DeadLetterQueue.Should().Be("dlq");
        opts.DefaultMessageTTL.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        // Arrange
        var opts = new RabbitMqOptions();

        // Act
        var act = () => opts.Validate();

        // Assert - 默认值合法（EnableDeadLetter=false，不校验死信项）
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_EnableDeadLetterWithCompleteConfig_DoesNotThrow()
    {
        // Arrange
        var opts = new RabbitMqOptions
        {
            EnableDeadLetter = true,
            DeadLetterExchange = "dlx",
            DeadLetterQueue = "dlq",
            DeadLetterRoutingKey = "dlk"
        };

        // Act
        var act = () => opts.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(nameof(RabbitMqOptions.ChannelPoolSize), 0)]
    [InlineData(nameof(RabbitMqOptions.ConsumerChannelPoolSize), 0)]
    [InlineData(nameof(RabbitMqOptions.MaxRetryCount), 0)]
    [InlineData(nameof(RabbitMqOptions.ConsumerMaxRetryCount), 0)]
    [InlineData(nameof(RabbitMqOptions.OutboxBatchSize), 0)]
    public void Validate_NonPositiveNumericFields_Throw(string fieldName, int badValue)
    {
        // Arrange
        var opts = new RabbitMqOptions();
        typeof(RabbitMqOptions).GetProperty(fieldName)!.SetValue(opts, badValue);

        // Act
        var act = () => opts.Validate();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyHostName_Throws(string host)
    {
        var opts = new RabbitMqOptions { HostName = host };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-1)]
    public void Validate_PortOutOfRange_Throws(int port)
    {
        var opts = new RabbitMqOptions { Port = port };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_PrefetchCountZero_Throws()
    {
        // PrefetchCount 为 ushort，0 在 AMQP 为无限（流控失效），库契约不允许
        var opts = new RabbitMqOptions { PrefetchCount = 0 };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_NegativeRetryBaseDelay_Throws()
    {
        var opts = new RabbitMqOptions { RetryBaseDelay = TimeSpan.FromSeconds(-1) };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_ZeroRetryMaxDelay_Throws()
    {
        var opts = new RabbitMqOptions { RetryMaxDelay = TimeSpan.Zero };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_EnableDeadLetterWithEmptyExchange_Throws()
    {
        var opts = new RabbitMqOptions
        {
            EnableDeadLetter = true,
            DeadLetterExchange = "",
            DeadLetterQueue = "dlq",
            DeadLetterRoutingKey = "dlk"
        };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_TooLargeMessageTTL_Throws()
    {
        // Arrange - 超过 int.MaxValue 毫秒（约 24.85 天）溢出 x-message-ttl
        var opts = new RabbitMqOptions { DefaultMessageTTL = TimeSpan.FromDays(30) };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }
}
