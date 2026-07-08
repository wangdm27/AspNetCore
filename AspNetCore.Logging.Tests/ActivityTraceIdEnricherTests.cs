using System.Diagnostics;

using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using AspNetCore.Logging.Enrichers;

namespace AspNetCore.Logging.Tests;

/// <summary>
/// ActivityTraceIdEnricher 单元测试：从 Activity.Current 附加 TraceId/SpanId。
/// </summary>
public class ActivityTraceIdEnricherTests : IDisposable
{
    private sealed class SimplePropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }

    private readonly ActivitySource _source = new("AspNetCore.Logging.Tests.Enricher");
    private readonly ActivityListener _listener;
    private readonly ActivityTraceIdEnricher _enricher = new();
    private readonly LogEvent _logEvent;
    private readonly ILogEventPropertyFactory _propertyFactory = new SimplePropertyFactory();

    public ActivityTraceIdEnricherTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
        _logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("msg", Array.Empty<MessageTemplateToken>()),
            Array.Empty<LogEventProperty>());
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        Activity.Current = null;
    }

    [Fact]
    public void Enrich_WithActiveActivity_AddsTraceIdAndSpanId()
    {
        // Arrange
        using var activity = _source.StartActivity("root", ActivityKind.Internal);
        activity!.Start();
        var expectedTrace = activity.TraceId.ToHexString();
        var expectedSpan = activity.SpanId.ToHexString();

        // Act
        _enricher.Enrich(_logEvent, _propertyFactory);

        // Assert
        _logEvent.Properties.Should().ContainKey("TraceId");
        _logEvent.Properties["TraceId"].ToString().Should().Contain(expectedTrace);
        _logEvent.Properties.Should().ContainKey("SpanId");
        _logEvent.Properties["SpanId"].ToString().Should().Contain(expectedSpan);
    }

    [Fact]
    public void Enrich_WithNoActiveActivity_AddsEmptyTraceIdAndSpanId()
    {
        // Arrange
        Activity.Current = null;

        // Act
        _enricher.Enrich(_logEvent, _propertyFactory);

        // Assert — 无 Activity 时输出空字符串
        _logEvent.Properties.Should().ContainKey("TraceId");
        _logEvent.Properties["TraceId"].ToString().Should().Be("\"\"");
        _logEvent.Properties.Should().ContainKey("SpanId");
        _logEvent.Properties["SpanId"].ToString().Should().Be("\"\"");
    }

    [Fact]
    public void Enrich_PreservesActivityTraceIdHexString()
    {
        // Arrange — 验证 hex 格式与 Activity.TraceId.ToHexString 完全一致
        using var activity = _source.StartActivity("root", ActivityKind.Internal);
        activity!.Start();

        // Act
        _enricher.Enrich(_logEvent, _propertyFactory);

        // Assert
        var traceProp = ((ScalarValue)_logEvent.Properties["TraceId"]).Value as string;
        traceProp.Should().Be(activity.TraceId.ToHexString());
    }
}
