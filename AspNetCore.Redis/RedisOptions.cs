namespace AspNetCore.Redis;

/// <summary>
/// Redis 配置选项类，用于配置 Redis 连接参数
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Redis 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Redis 数据库索引，默认为 0
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Redis 键前缀，默认为 "app:"，用于区分不同应用或模块的 Redis 键
    /// </summary>
    public string KeyPrefix { get; set; } = "app:";
}