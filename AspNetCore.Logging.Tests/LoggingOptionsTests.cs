using FluentAssertions;
using Serilog.Events;

using AspNetCore.Logging;

namespace AspNetCore.Logging.Tests;

/// <summary>
/// LoggingOptions / LoggingSinksOptions / LoggingEnrichmentOptions POCO 默认值与回读单元测试。
/// </summary>
public class LoggingOptionsTests
{
    [Fact]
    public void LoggingOptions_Defaults_HaveExpectedValues()
    {
        // Act
        var opts = new LoggingOptions();

        // Assert
        opts.ApplicationName.Should().Be("app");
        opts.MinimumLevel.Should().Be(LogEventLevel.Information);
        opts.Sinks.Should().NotBeNull();
        opts.Enrichment.Should().NotBeNull();
    }

    [Fact]
    public void LoggingOptions_AssignedValues_RoundTripPreserved()
    {
        // Arrange
        var sinks = new LoggingSinksOptions { EnableSeq = false, SeqUrl = "http://seq:1234" };
        var enrichment = new LoggingEnrichmentOptions { EnableTraceId = false };

        // Act
        var opts = new LoggingOptions
        {
            ApplicationName = "api",
            MinimumLevel = LogEventLevel.Debug,
            Sinks = sinks,
            Enrichment = enrichment
        };

        // Assert
        opts.ApplicationName.Should().Be("api");
        opts.MinimumLevel.Should().Be(LogEventLevel.Debug);
        opts.Sinks.Should().BeSameAs(sinks);
        opts.Enrichment.Should().BeSameAs(enrichment);
    }

    [Fact]
    public void LoggingSinksOptions_Defaults_HaveExpectedValues()
    {
        // Act
        var opts = new LoggingSinksOptions();

        // Assert
        opts.EnableConsole.Should().BeTrue();
        opts.EnableFile.Should().BeTrue();
        opts.EnableSeq.Should().BeTrue();
        opts.SeqUrl.Should().Be("http://localhost:5341");
        opts.FileBasePath.Should().Be("logs");
        opts.FileRetainedFileCountLimit.Should().Be(14);
        opts.FileSizeLimitBytes.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public void LoggingSinksOptions_AssignedValues_RoundTripPreserved()
    {
        // Act
        var opts = new LoggingSinksOptions
        {
            EnableConsole = false,
            EnableFile = false,
            EnableSeq = false,
            SeqUrl = "http://seq:5341",
            FileBasePath = "/var/logs",
            FileRetainedFileCountLimit = null,
            FileSizeLimitBytes = null
        };

        // Assert
        opts.EnableConsole.Should().BeFalse();
        opts.EnableFile.Should().BeFalse();
        opts.EnableSeq.Should().BeFalse();
        opts.SeqUrl.Should().Be("http://seq:5341");
        opts.FileBasePath.Should().Be("/var/logs");
        opts.FileRetainedFileCountLimit.Should().BeNull();
        opts.FileSizeLimitBytes.Should().BeNull();
    }

    [Fact]
    public void LoggingEnrichmentOptions_Defaults_AllEnabled()
    {
        // Act
        var opts = new LoggingEnrichmentOptions();

        // Assert
        opts.EnableTraceId.Should().BeTrue();
        opts.EnableUserContext.Should().BeTrue();
        opts.EnableMachineName.Should().BeTrue();
        opts.EnableThreadId.Should().BeTrue();
    }

    [Fact]
    public void LoggingEnrichmentOptions_AssignedValues_RoundTripPreserved()
    {
        // Act
        var opts = new LoggingEnrichmentOptions
        {
            EnableTraceId = false,
            EnableUserContext = false,
            EnableMachineName = false,
            EnableThreadId = false
        };

        // Assert
        opts.EnableTraceId.Should().BeFalse();
        opts.EnableUserContext.Should().BeFalse();
        opts.EnableMachineName.Should().BeFalse();
        opts.EnableThreadId.Should().BeFalse();
    }

    [Fact]
    public void LoggingOptions_SinksAndEnrichment_IndependentInstances()
    {
        // Act — 两个 LoggingOptions 实例应有独立的 Sinks/Enrichment
        var a = new LoggingOptions();
        var b = new LoggingOptions();

        // Assert
        a.Sinks.Should().NotBeSameAs(b.Sinks);
        a.Enrichment.Should().NotBeSameAs(b.Enrichment);
    }
}
