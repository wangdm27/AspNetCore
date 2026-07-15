using System.Diagnostics;

using FluentAssertions;

using AspNetCore.RabbitMq;

using Moq;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqPublisher 单元测试。internal sealed，经 InternalsVisibleTo 可见。
/// mock IRabbitMqChannelPool + IChannel，confirm=false 跳过确认逻辑。
/// 覆盖 H1（traceparent 不覆盖 outbox 父链路）与 M6（props 回调替换 Headers 时入参/x-delay 补缺）。
/// </summary>
public class RabbitMqPublisherTests : IDisposable
{
    private readonly ActivitySource _source = new("AspNetCore.RabbitMq.Tests");
    private readonly ActivityListener _listener;
    private BasicProperties? _capturedProps;

    public RabbitMqPublisherTests()
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

    /// <summary>构造通道池 mock：RentAsync 返回包裹 mock IChannel 的租约，BasicPublishAsync 捕获 basicProperties。
    /// IRabbitMqChannelPoolLease 为 internal，手写 fake 实现（Moq 无法代理非 DynamicProxyGenAssembly2 可见的 internal）。</summary>
    private Mock<IRabbitMqChannelPool> SetupPool()
    {
        _capturedProps = null;

        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.BasicPublishAsync<BasicProperties>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>((_, _, _, props, _, _) => _capturedProps = props)
            .Returns(ValueTask.CompletedTask);

        var lease = new PooledChannelLease(channelMock.Object, null, new NoOpLease());

        var poolMock = new Mock<IRabbitMqChannelPool>();
        poolMock.Setup(p => p.RentAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PooledChannelLease>(lease));
        return poolMock;
    }

    /// <summary>归还 lease 的空实现：发布者测试不关心归还，仅满足租约 dispose。</summary>
    private sealed class NoOpLease : IRabbitMqChannelPoolLease
    {
        public ValueTask ReturnAsync(IChannel channel, ChannelConfirmTracker? tracker) => ValueTask.CompletedTask;
    }

    private void AssertTraceparent(string expected)
    {
        _capturedProps.Should().NotBeNull();
        _capturedProps!.Headers.Should().NotBeNull();
        _capturedProps.Headers!.Should().ContainKey(RabbitMqTracing.TraceParentHeader);
        ((string)_capturedProps!.Headers![RabbitMqTracing.TraceParentHeader]!).Should().Be(expected);
    }

    [Fact]
    public async Task PublishRawAsync_WhenHeadersContainTraceparent_DoesNotOverwriteWithCurrentActivity()
    {
        // Arrange - Activity.Current 非 null（Inject 会想写入当前 traceparent），但 headers 已携带 outbox 的 traceparent
        using var current = _source.StartActivity("current", ActivityKind.Producer);
        current!.Start();
        var currentTp = $"00-{current.TraceId.ToHexString()}-{current.SpanId.ToHexString()}-01";
        const string preset = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01";
        preset.Should().NotBe(currentTp);

        var poolMock = SetupPool();
        var publisher = new RabbitMqPublisher(poolMock.Object, new RabbitMqOptions());
        var headers = new Dictionary<string, object?> { [RabbitMqTracing.TraceParentHeader] = preset };

        // Act
        await publisher.PublishRawAsync("ex", "rk", new byte[] { 1 }, headers, confirm: false);

        // Assert - 保留预设 traceparent，未被当前 Activity 覆盖（H1 outbox 父链路保护）
        AssertTraceparent(preset);
    }

    [Fact]
    public async Task PublishRawAsync_WhenNoTraceparent_InjectsCurrentActivity()
    {
        // Arrange - 直发路径：headers 无 traceparent，注入当前 Activity
        using var current = _source.StartActivity("current", ActivityKind.Producer);
        current!.Start();
        var currentTp = $"00-{current.TraceId.ToHexString()}-{current.SpanId.ToHexString()}-01";

        var poolMock = SetupPool();
        var publisher = new RabbitMqPublisher(poolMock.Object, new RabbitMqOptions());

        // Act
        await publisher.PublishRawAsync("ex", "rk", new byte[] { 1 }, headers: null, confirm: false);

        // Assert - 注入当前 Activity 的 traceparent
        AssertTraceparent(currentTp);
    }

    [Fact]
    public async Task PublishRawAsync_MergesHeadersWhenPropsCallbackReplacesHeaders()
    {
        // Arrange - 无 Activity，避免 traceparent 干扰；M6：props 回调整体替换 Headers
        Activity.Current = null;
        var poolMock = SetupPool();
        var publisher = new RabbitMqPublisher(poolMock.Object, new RabbitMqOptions());
        var incoming = new Dictionary<string, object?> { ["k1"] = "v1" };

        // Act - props 回调把 Headers 整体换成 {k2}，入参 k1 与 x-delay 应补缺保留
        await publisher.PublishRawAsync("ex", "rk", new byte[] { 1 }, incoming,
            props: p => p.Headers = new Dictionary<string, object?> { ["k2"] = "v2" },
            confirm: false, delayMs: 100);

        // Assert - k1（入参）、k2（回调）、x-delay（延迟头）均在，未被回调整体替换丢失
        _capturedProps.Should().NotBeNull();
        _capturedProps!.Headers.Should().NotBeNull();
        _capturedProps.Headers!.Should().ContainKey("k1");
        _capturedProps.Headers.Should().ContainKey("k2");
        _capturedProps.Headers.Should().ContainKey("x-delay");
    }
}
