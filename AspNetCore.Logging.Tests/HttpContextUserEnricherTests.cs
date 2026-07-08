using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using AspNetCore.Logging;
using AspNetCore.Logging.Enrichers;

namespace AspNetCore.Logging.Tests;

/// <summary>
/// HttpContextUserEnricher 单元测试：UserId/TenantId 附加，Provider 静态绑定。
/// </summary>
public class HttpContextUserEnricherTests
{
    private sealed class FakeUserContextProvider : IUserContextProvider
    {
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
    }

    private sealed class SimplePropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }

    private readonly HttpContextUserEnricher _enricher = new();
    private readonly ILogEventPropertyFactory _propertyFactory = new SimplePropertyFactory();

    private static LogEvent NewLogEvent()
        => new(DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("msg", Array.Empty<MessageTemplateToken>()),
            Array.Empty<LogEventProperty>());

    [Fact]
    public void Enrich_WithNullProvider_DoesNotAddUserProperties()
    {
        // Arrange
        HttpContextUserEnricher.Provider = null;
        var logEvent = NewLogEvent();

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_WithUserIdAndTenantId_AddsBothProperties()
    {
        // Arrange
        HttpContextUserEnricher.Provider = new FakeUserContextProvider
        {
            UserId = "u-1",
            TenantId = "t-1"
        };
        var logEvent = NewLogEvent();

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties["UserId"].ToString().Should().Contain("u-1");
        logEvent.Properties.Should().ContainKey("TenantId");
        logEvent.Properties["TenantId"].ToString().Should().Contain("t-1");
    }

    [Fact]
    public void Enrich_WithOnlyUserId_AddsOnlyUserId()
    {
        // Arrange
        HttpContextUserEnricher.Provider = new FakeUserContextProvider
        {
            UserId = "u-2",
            TenantId = null
        };
        var logEvent = NewLogEvent();

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_WithOnlyTenantId_AddsOnlyTenantId()
    {
        // Arrange
        HttpContextUserEnricher.Provider = new FakeUserContextProvider
        {
            UserId = null,
            TenantId = "t-3"
        };
        var logEvent = NewLogEvent();

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
        logEvent.Properties.Should().ContainKey("TenantId");
    }

    [Fact]
    public void Enrich_WithEmptyStrings_DoesNotAddProperties()
    {
        // Arrange — 空字符串视为无值，跳过
        HttpContextUserEnricher.Provider = new FakeUserContextProvider
        {
            UserId = string.Empty,
            TenantId = string.Empty
        };
        var logEvent = NewLogEvent();

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_AddPropertyIfAbsent_DoesNotOverwriteExisting()
    {
        // Arrange — 已存在 TraceId 不应被覆盖（AddPropertyIfAbsent 语义）
        HttpContextUserEnricher.Provider = new FakeUserContextProvider
        {
            UserId = "new-user",
            TenantId = null
        };
        var logEvent = NewLogEvent();
        logEvent.AddOrUpdateProperty(_propertyFactory.CreateProperty("UserId", "old-user"));

        // Act
        _enricher.Enrich(logEvent, _propertyFactory);

        // Assert
        logEvent.Properties["UserId"].ToString().Should().Contain("old-user");
    }
}
