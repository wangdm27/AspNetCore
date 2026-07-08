using FluentAssertions;
using Microsoft.Extensions.Configuration;

using AspNetCore.Scheduler.Infrastructure;

using Moq;

namespace AspNetCore.Scheduler.Tests;

/// <summary>
/// HangfireDbInitializer 单元测试：配置门控与异常路径（不连真实 PG）。
/// EnsureDatabaseAsync 分支：AutoCreateDatabase!=true 直接返回；连接串缺失抛异常。
/// 注：ExtractDatabaseName/IsValidDbName 已改为 internal，纯逻辑覆盖见 HangfireDbInitializerDbNameTests。
/// 用 mock IConfiguration/IConfigurationSection 避免引 Memory 配置包。
/// </summary>
public class HangfireDbInitializerTests
{
    /// <summary>构造 mock IConfiguration，模拟 Hangfire 节与连接串读取。</summary>
    private static IConfiguration BuildConfig(
        string? autoCreateRaw = null,
        string? hangfireCs = null,
        string? adminCs = null)
    {
        // GetValue<bool?>("AutoCreateDatabase") 内部调 GetSection("AutoCreateDatabase").Get<bool?>()
        // Get<bool?>() 读 section.Value，故需子节带 Value
        var autoCreateSectionMock = new Mock<IConfigurationSection>();
        autoCreateSectionMock.SetupGet(s => s.Value).Returns(autoCreateRaw);
        autoCreateSectionMock.Setup(s => s["AutoCreateDatabase"]).Returns(autoCreateRaw);

        // Hangfire 节：hf["AdminConnectionString"] 与 GetSection("AutoCreateDatabase")
        var hfSectionMock = new Mock<IConfigurationSection>();
        hfSectionMock.Setup(s => s["AutoCreateDatabase"]).Returns(autoCreateRaw);
        hfSectionMock.Setup(s => s["AdminConnectionString"]).Returns(adminCs);
        hfSectionMock.Setup(s => s.GetSection("AutoCreateDatabase")).Returns(autoCreateSectionMock.Object);

        // ConnectionStrings 节：GetConnectionString(name) 读 GetSection("ConnectionStrings")[name]
        var csSectionMock = new Mock<IConfigurationSection>();
        csSectionMock.Setup(s => s["HangfirePostgreSql"]).Returns(hangfireCs);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("Hangfire")).Returns(hfSectionMock.Object);
        configMock.Setup(c => c.GetSection("ConnectionStrings")).Returns(csSectionMock.Object);
        return configMock.Object;
    }

    [Fact]
    public async Task EnsureDatabaseAsync_WhenAutoCreateDisabled_ReturnsWithoutError()
    {
        // Arrange - AutoCreateDatabase=false 时不建库
        var cfg = BuildConfig(autoCreateRaw: "False");

        // Act
        var act = () => HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        // Assert - 不连库，直接返回
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureDatabaseAsync_WhenAutoCreateNull_ReturnsWithoutError()
    {
        // Arrange - 未配置 AutoCreateDatabase（null）
        var cfg = BuildConfig(autoCreateRaw: null);

        // Act
        var act = () => HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureDatabaseAsync_WhenAutoCreateEnabledButNoHangfireCs_Throws()
    {
        // Arrange - 开启建库但缺 HangfirePostgreSql 连接串
        var cfg = BuildConfig(
            autoCreateRaw: "True",
            hangfireCs: null,
            adminCs: "Host=localhost;Database=postgres");

        // Act
        var act = () => HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("HangfirePostgreSql");
    }

    [Fact]
    public async Task EnsureDatabaseAsync_WhenAutoCreateEnabledButNoAdminCs_Throws()
    {
        // Arrange - 有目标库连接串但缺 AdminConnectionString
        var cfg = BuildConfig(
            autoCreateRaw: "True",
            hangfireCs: "Host=localhost;Database=mydb",
            adminCs: null);

        // Act
        var act = () => HangfireDbInitializer.EnsureDatabaseAsync(cfg);

        // Assert - 缺 AdminConnectionString 抛异常（在此分支前未连库）
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("AdminConnectionString");
    }
}
