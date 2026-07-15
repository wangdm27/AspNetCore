using System.Diagnostics;

using FluentAssertions;

using AspNetCore.RabbitMq;

using Moq;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqOutbox 单元测试。internal sealed，经 InternalsVisibleTo 可见。
/// 覆盖 H1（入队捕获 traceparent）、M5（持久化 BasicProperties）、L12（exchange/routingKey 校验）。
/// </summary>
public class RabbitMqOutboxTests : IDisposable
{
    private readonly ActivitySource _source = new("AspNetCore.RabbitMq.Tests");
    private readonly ActivityListener _listener;
    private RabbitMqOutboxMessage? _captured;

    public RabbitMqOutboxTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        Activity.Current = null;
    }

    /// <summary>构造 outbox，AddAsync 捕获入队消息。</summary>
    private RabbitMqOutbox CreateOutbox()
    {
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        storeMock.Setup(s => s.AddAsync(It.IsAny<RabbitMqOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RabbitMqOutboxMessage, CancellationToken>((m, _) => _captured = m)
            .Returns(Task.CompletedTask);
        return new RabbitMqOutbox(storeMock.Object);
    }

    [Fact]
    public async Task EnqueueAsync_WithActiveActivity_CapturesTraceparentIntoHeaders()
    {
        // Arrange - 请求上下文有 Activity（H1：必须在此处捕获，dispatcher 后台发布时 Activity.Current 已非原请求）
        using var activity = _source.StartActivity("request", ActivityKind.Producer);
        activity!.Start();
        var traceId = activity.TraceId;

        var outbox = CreateOutbox();

        // Act
        await outbox.EnqueueAsync("orders", "created", new { id = 1 });

        // Assert - 入队消息 Headers 含 traceparent，TraceId 与请求一致
        _captured.Should().NotBeNull();
        _captured!.Headers.Should().ContainKey(RabbitMqTracing.TraceParentHeader);
        var tp = (string)_captured.Headers[RabbitMqTracing.TraceParentHeader]!;
        tp.Split('-')[1].Should().Be(traceId.ToHexString());
    }

    [Fact]
    public async Task EnqueueAsync_WithNoActivity_HeadersHaveNoTraceparent()
    {
        // Arrange - 无 Activity：不注入 traceparent（dispatcher 场景不应走到这里，但行为应安全）
        Activity.Current = null;
        var outbox = CreateOutbox();

        // Act
        await outbox.EnqueueAsync("orders", "created", new { id = 1 });

        // Assert
        _captured.Should().NotBeNull();
        _captured!.Headers.Should().NotContainKey(RabbitMqTracing.TraceParentHeader);
    }

    [Fact]
    public async Task EnqueueAsync_PreservesBasicPropertiesFromPropsCallback()
    {
        // Arrange - M5：props 回调设置的 ContentType/CorrelationId/MessageId 应持久化进 outbox
        var outbox = CreateOutbox();

        // Act
        await outbox.EnqueueAsync("orders", "created", new { id = 1 }, props: p =>
        {
            p.ContentType = "application/json";
            p.CorrelationId = "corr-123";
            p.MessageId = "msg-456";
        });

        // Assert
        _captured.Should().NotBeNull();
        _captured!.ContentType.Should().Be("application/json");
        _captured.CorrelationId.Should().Be("corr-123");
        _captured.MessageId.Should().Be("msg-456");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EnqueueAsync_WithNullOrEmptyExchange_Throws(string? exchange)
    {
        var outbox = CreateOutbox();
        var act = () => outbox.EnqueueAsync(exchange!, "created", new { id = 1 }).AsTask();
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EnqueueAsync_WithNullOrEmptyRoutingKey_Throws(string? routingKey)
    {
        var outbox = CreateOutbox();
        var act = () => outbox.EnqueueAsync("orders", routingKey!, new { id = 1 }).AsTask();
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
