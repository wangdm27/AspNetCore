using System.Text.RegularExpressions;
using Npgsql;

namespace AspNetCore.Scheduler.Infrastructure;

/// <summary>
/// 自动建库。PostgreSQL 不支持 CREATE DATABASE IF NOT EXISTS,
/// 故连系统库 postgres 检查目标库存在性,不存在则 CREATE。
/// Hangfire schema 由 storage 首启自动建 (UsePostgreSqlStorage),无需此处处理。
/// </summary>
public static class HangfireDbInitializer
{
    public static async Task EnsureDatabaseAsync(IConfiguration cfg, CancellationToken ct = default)
    {
        var hf = cfg.GetSection("Hangfire");
        if (hf.GetValue<bool?>("AutoCreateDatabase") != true) return;

        var target = cfg.GetConnectionString("HangfirePostgreSql")
            ?? throw new InvalidOperationException("HangfirePostgreSql 连接串缺失");
        var admin = hf["AdminConnectionString"]
            ?? throw new InvalidOperationException("Hangfire:AdminConnectionString 缺失");

        var dbName = ExtractDatabaseName(target);
        if (string.IsNullOrEmpty(dbName)) return;

        await using var conn = new NpgsqlConnection(admin);
        await conn.OpenAsync(ct);

        // 幂等: 库不存在才建
        const string checkSql = "SELECT 1 FROM pg_database WHERE datname = @db";
        await using var check = new NpgsqlCommand(checkSql, conn);
        check.Parameters.AddWithValue("@db", dbName);
        var exists = await check.ExecuteScalarAsync(ct);
        if (exists is not null) return;

        // CREATE DATABASE 不支持参数化标识符,需校验库名防注入
        if (!IsValidDbName(dbName))
            throw new InvalidOperationException($"非法库名: {dbName}");

        await using var create = new NpgsqlCommand(
            $"CREATE DATABASE \"{dbName}\"", conn);
        await create.ExecuteNonQueryAsync(ct);
    }

    /// <summary>从 Npgsql 连接串提取 Database 段。</summary>
    private static string? ExtractDatabaseName(string connStr)
    {
        // 兼容 Database=xxx 与 database=xxx
        var match = Regex.Match(connStr, @"[Dd]atabase=([^;]+)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>库名仅允许字母数字下划线,防 SQL 注入 (CREATE DATABASE 不支持参数化)。</summary>
    private static bool IsValidDbName(string name)
        => Regex.IsMatch(name, @"^[A-Za-z0-9_]+$");
}
