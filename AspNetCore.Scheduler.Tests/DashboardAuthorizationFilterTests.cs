using FluentAssertions;

using AspNetCore.Scheduler.Infrastructure.Extensions;

using Hangfire.Dashboard;

namespace AspNetCore.Scheduler.Tests;

/// <summary>
/// DashboardAuthorizationFilter 单元测试：Authorize 默认拒绝（生产授权点）。
/// 注：Authorize 不读 context 内容，传 null 即可验证默认拒绝逻辑。
/// </summary>
public class DashboardAuthorizationFilterTests
{
    [Fact]
    public void Authorize_WithAnyContext_ReturnsFalse()
    {
        // Arrange - 默认拒绝；Authorize 实现不读 context，传 null
        var filter = new DashboardAuthorizationFilter();

        // Act
        var result = filter.Authorize(null!);

        // Assert - 生产环境默认拒绝
        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_Always_ReturnsFalse()
    {
        // Arrange - 多次调用均拒绝（占位实现）
        var filter = new DashboardAuthorizationFilter();

        // Act & Assert
        filter.Authorize(null!).Should().BeFalse();
        filter.Authorize(null!).Should().BeFalse();
    }

    [Fact]
    public void DashboardAuthorizationFilter_ImplementsIDashboardAuthorizationFilter()
    {
        // Arrange
        var filter = new DashboardAuthorizationFilter();

        // Assert
        filter.Should().BeAssignableTo<IDashboardAuthorizationFilter>();
    }
}
