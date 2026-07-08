using System.Diagnostics;
using System.Text;

using FluentAssertions;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqTracing 单元测试：W3C traceparent 注入/提取，纯字符串/Activity 逻辑。
/// internal 类经 InternalsVisibleTo 暴露。
/// </summary>
public class RabbitMqTracingTests : IDisposable
{
    private readonly ActivitySource _source = new("AspNetCore.RabbitMq.Tests");
    private readonly ActivityListener _listener;

    public RabbitMqTracingTests()
    {
        // 启用全数据采样监听，使 StartActivity 创建真实 Activity（默认无监听器返回 null）
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        Activity.Current = null;
    }

    [Fact]
    public void Inject_WithActiveActivity_WritesTraceparentHeader()
    {
        // Arrange — 创建一个带父上下文的 Activity 以保证 TraceId 非默认
        using var activity = _source.StartActivity("root", ActivityKind.Producer);
        activity.Should().NotBeNull();
        activity!.Start();

        var headers = new Dictionary<string, object?>();

        // Act
        RabbitMqTracing.Inject(headers);

        // Assert
        headers.Should().ContainKey("traceparent");
        var tp = (string)headers["traceparent"]!;
        var parts = tp.Split('-');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("00");
        parts[1].Should().Be(activity.TraceId.ToHexString());
        parts[2].Should().Be(activity.SpanId.ToHexString());
        parts[3].Should().Be("01");
    }

    [Fact]
    public void Inject_WithNoCurrentActivity_DoesNotModifyHeaders()
    {
        // Arrange
        Activity.Current = null;
        var headers = new Dictionary<string, object?>();

        // Act
        RabbitMqTracing.Inject(headers);

        // Assert
        headers.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAndStartActivity_WithValidTraceparentString_CreatesActivityWithParentTraceId()
    {
        // Arrange — 模拟发布端 traceparent
        using var producer = _source.StartActivity("producer", ActivityKind.Producer);
        producer!.Start();
        var producerTraceId = producer.TraceId;
        var producerSpanId = producer.SpanId;
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = $"00-{producerTraceId.ToHexString()}-{producerSpanId.ToHexString()}-01"
        };
        Activity.Current = null;

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert — 消费端延续发布端 TraceId
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().Be(producerTraceId);
    }

    [Fact]
    public void ExtractAndStartActivity_WithByteArrayHeader_CreatesActivityWithParentTraceId()
    {
        // Arrange — RabbitMQ 头值可能为 byte[]（UTF-8）
        using var producer = _source.StartActivity("producer", ActivityKind.Producer);
        producer!.Start();
        var producerTraceId = producer.TraceId;
        var tp = $"00-{producerTraceId.ToHexString()}-{producer.SpanId.ToHexString()}-01";
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes(tp)
        };
        Activity.Current = null;

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().Be(producerTraceId);
    }

    [Fact]
    public void ExtractAndStartActivity_WithNullHeaders_CreatesIndependentActivity()
    {
        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(null);

        // Assert — 无父链路时仍创建独立 Activity
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().NotBe(default);
    }

    [Fact]
    public void ExtractAndStartActivity_WithMissingTraceparentHeader_CreatesIndependentActivity()
    {
        // Arrange
        var headers = new Dictionary<string, object?>();

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().NotBe(default);
    }

    [Fact]
    public void ExtractAndStartActivity_WithMalformedTraceparent_CreatesIndependentActivity()
    {
        // Arrange — 非 4 段、非 00 版本、非法 hex 均应被拒绝，转独立 Activity
        var headers = new Dictionary<string, object?> { ["traceparent"] = "garbage" };
        Activity.Current = null;

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert — 解析失败仍创建独立 Activity
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().NotBe(default);
    }

    [Theory]
    [InlineData("bad")]                     // 非 4 段
    [InlineData("01-aaaa-bbbb-01")]         // 版本非 00
    [InlineData("00-xyz-bbbb-01")]          // traceId 非 32hex
    [InlineData("00-aaaa-xyz-01")]          // spanId 非 16hex
    [InlineData("00-aaaa-bbbb-01-extra")]   // 超过 4 段
    public void ExtractAndStartActivity_WithInvalidTraceparents_CreatesIndependentActivity(string bad)
    {
        // Arrange
        var headers = new Dictionary<string, object?> { ["traceparent"] = bad };
        Activity.Current = null;

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert
        consumer.Should().NotBeNull();
    }

    [Fact]
    public void Inject_ThenExtract_PreservesTraceIdAcrossBoundary()
    {
        // Arrange — 端到端：发布端注入 → 消费端提取
        using var producer = _source.StartActivity("producer", ActivityKind.Producer);
        producer!.Start();
        var producerTraceId = producer.TraceId;
        var headers = new Dictionary<string, object?>();
        RabbitMqTracing.Inject(headers);
        Activity.Current = null;

        // Act
        using var consumer = RabbitMqTracing.ExtractAndStartActivity(headers);

        // Assert — 跨进程 TraceId 一致（Seq 可按 TraceId 串联）
        consumer.Should().NotBeNull();
        consumer!.TraceId.Should().Be(producerTraceId);
    }
}
