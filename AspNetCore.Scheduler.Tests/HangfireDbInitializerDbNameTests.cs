using FluentAssertions;

using AspNetCore.Scheduler.Infrastructure;

namespace AspNetCore.Scheduler.Tests;

/// <summary>
/// HangfireDbInitializer.ExtractDatabaseName / IsValidDbName 纯逻辑单元测试。
/// 二者已由 private 改为 internal（配合 InternalsVisibleTo），可直接测。
/// EnsureDatabaseAsync 门控分支已在 HangfireDbInitializerTests 覆盖，此处不重复。
/// </summary>
public class HangfireDbInitializerDbNameTests
{
    // ---------- ExtractDatabaseName ----------

    [Theory]
    [InlineData("Host=localhost;Database=mydb;Port=5432", "mydb")]      // 标准大写 Database
    [InlineData("Host=localhost;database=mydb;Port=5432", "mydb")]      // 小写 database 兼容
    [InlineData("Database=foo;Host=bar", "foo")]                         // 分号截取
    [InlineData("Database=  spaced  ;Host=bar", "spaced")]              // 前后空格 trim
    [InlineData("Database=mydb", "mydb")]                                // 仅一段
    public void ExtractDatabaseName_ValidSegment_ReturnsTrimmedName(string connStr, string expected)
    {
        var result = HangfireDbInitializer.ExtractDatabaseName(connStr);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Host=localhost", null)]          // 无 Database 段
    [InlineData("", null)]                        // 空串
    [InlineData("Database=;Host=bar", null)]      // Database= 后立即分号，[^;]+ 不匹配
    [InlineData("DATABASE=mydb", null)]           // 全大写：正则仅 [Dd]atabase，不匹配 DATABASE
    public void ExtractDatabaseName_NoMatch_ReturnsNull(string connStr, string? expected)
    {
        var result = HangfireDbInitializer.ExtractDatabaseName(connStr);
        result.Should().Be(expected);
    }

    // ---------- IsValidDbName ----------

    [Theory]
    [InlineData("mydb")]              // 纯小写字母
    [InlineData("MyDB_123")]          // 混合字母数字下划线
    [InlineData("ABC")]               // 纯大写
    [InlineData("a1_b2")]             // 交替
    [InlineData("_underscores_only_")]// 下划线边界
    public void IsValidDbName_LegalNames_ReturnsTrue(string name)
    {
        HangfireDbInitializer.IsValidDbName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("my db")]             // 空格
    [InlineData("my-db")]             // 连字符
    [InlineData("my.db")]             // 点号
    [InlineData("foo'; DROP--")]      // SQL 注入尝试（单引号/分号/空格/连字符）
    [InlineData("foo\"; DROP--")]     // 双引号注入
    [InlineData("db;")]               // 分号
    [InlineData("")]                  // 空串（+ 需要 1 个以上字符）
    [InlineData("有中文")]             // 非 ASCII
    public void IsValidDbName_InvalidNames_ReturnsFalse(string name)
    {
        HangfireDbInitializer.IsValidDbName(name).Should().BeFalse();
    }

    [Fact]
    public void IsValidDbName_Null_ThrowsArgumentNullException()
    {
        // 源码透传 Regex.IsMatch(null, ...) 抛 ArgumentNullException（不返回 false）。
        // 生产调用方 EnsureDatabaseAsync 已用 string.IsNullOrEmpty 提前拦截，故无实际风险。
        // 此处断言实际行为以锁定契约，而非臆测返回 false。
        Action act = () => HangfireDbInitializer.IsValidDbName(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
