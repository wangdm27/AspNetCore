using System.Diagnostics;

using FluentAssertions;

using AspNetCore.Logging;

namespace AspNetCore.Logging.Tests;

/// <summary>
/// ActivityTracingInitializer 单元测试：全局 ActivityListener 幂等启用。
/// internal 类经 InternalsVisibleTo 暴露。验证 Enable 后自定义 ActivitySource 能创建真实 Activity。
/// </summary>
public class ActivityTracingInitializerTests
{
    private const string TestSourceName = "AspNetCore.Logging.Tests.Tracing";

    [Fact]
    public void Enable_WhenCalled_AllowsActivitySourceToCreateActivities()
    {
        // Arrange — 启用全局监听
        ActivityTracingInitializer.Enable();

        using var source = new ActivitySource(TestSourceName);

        // Act
        using var activity = source.StartActivity("test", ActivityKind.Internal);

        // Assert — 无监听器时 StartActivity 返回 null；Enable 后应创建真实 Activity
        activity.Should().NotBeNull();
        activity!.TraceId.Should().NotBe(default);
    }

    [Fact]
    public void Enable_CalledMultipleTimes_IsIdempotentAndStillCreatesActivities()
    {
        // Arrange — 多次调用不应重复注册或抛异常
        ActivityTracingInitializer.Enable();
        ActivityTracingInitializer.Enable();
        ActivityTracingInitializer.Enable();

        using var source = new ActivitySource(TestSourceName + ".Idempotent");

        // Act
        using var activity = source.StartActivity("test2", ActivityKind.Internal);

        // Assert
        activity.Should().NotBeNull();
    }

    [Fact]
    public void Enable_AllActivitySources_Listened()
    {
        // Arrange — ShouldListenTo = _ => true，任意 source 都应被监听
        ActivityTracingInitializer.Enable();

        using var source1 = new ActivitySource("AspNetCore.Logging.Tests.A");
        using var source2 = new ActivitySource("AspNetCore.Logging.Tests.B");

        // Act
        using var a1 = source1.StartActivity("a1", ActivityKind.Internal);
        using var a2 = source2.StartActivity("a2", ActivityKind.Internal);

        // Assert — 两个不同 source 都被监听，均创建真实 Activity
        a1.Should().NotBeNull();
        a2.Should().NotBeNull();
        a1!.SpanId.Should().NotBe(a2!.SpanId);
    }

    [Fact]
    public void Enable_SamplingAllData_PreservesParentContext()
    {
        // Arrange — SampleUsingParentId / Sample 返回 AllData，父上下文应被保留
        ActivityTracingInitializer.Enable();

        using var source = new ActivitySource(TestSourceName + ".Parent");
        var parentId = ActivityTraceId.CreateRandom();
        var parentSpan = ActivitySpanId.CreateRandom();
        var parentContext = new ActivityContext(parentId, parentSpan, ActivityTraceFlags.Recorded);

        // Act
        using var activity = source.StartActivity("child", ActivityKind.Internal, parentContext);

        // Assert
        activity.Should().NotBeNull();
        activity!.TraceId.Should().Be(parentId);
        activity.ParentSpanId.Should().Be(parentSpan);
    }
}
