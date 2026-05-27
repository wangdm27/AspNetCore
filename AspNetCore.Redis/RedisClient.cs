using AspNetCore.Redis;
using StackExchange.Redis;

/// <summary>
/// Redis 客户端实现，基于 StackExchange.Redis 库提供 Redis 操作功能
/// </summary>
public class RedisClient : IRedisClient
{
    /// <summary>
    /// Redis 数据库实例
    /// </summary>
    private readonly IDatabase _db;
    /// <summary>
    /// Redis 序列化器
    /// </summary>
    private readonly IRedisSerializer _serializer;
    /// <summary>
    /// Redis 配置选项
    /// </summary>
    private readonly RedisOptions _options;

    /// <summary>
    /// 初始化 RedisClient 实例
    /// </summary>
    /// <param name="connection">Redis 连接多路复用器</param>
    /// <param name="serializer">Redis 序列化器</param>
    /// <param name="options">Redis 配置选项</param>
    public RedisClient(
        IConnectionMultiplexer connection,
        IRedisSerializer serializer,
        RedisOptions options)
    {
        _db = connection.GetDatabase(options.Database);
        _serializer = serializer;
        _options = options;
    }

    /// <summary>
    /// 构建带前缀的 Redis 键
    /// </summary>
    /// <param name="key">原始键</param>
    /// <returns>带前缀的键</returns>
    private string BuildKey(string key)
        => $"{_options.KeyPrefix}{key}";

    /// <summary>
    /// 设置指定键的值，并可选设置过期时间
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要设置的值</param>
    /// <param name="expiry">过期时间，为 null 表示使用默认过期时间</param>
    /// <returns>是否设置成功</returns>
    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var redisKey = BuildKey(key);
        var val = _serializer.Serialize(value);
        return await _db.StringSetAsync(redisKey, val, expiry ?? Expiration.Default);
    }

    /// <summary>
    /// 获取指定键的值
    /// </summary>
    /// <typeparam name="T">返回值的类型</typeparam>
    /// <param name="key">Redis 键</param>
    /// <returns>键对应的值，如果键不存在则返回 null</returns>
    public async Task<T?> GetAsync<T>(string key)
    {
        var redisKey = BuildKey(key);
        var val = await _db.StringGetAsync(redisKey);

        if (val.IsNullOrEmpty) return default;

        return _serializer.Deserialize<T>(val!);
    }

    /// <summary>
    /// 删除指定的键
    /// </summary>
    /// <param name="key">要删除的 Redis 键</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> RemoveAsync(string key)
    {
        return await _db.KeyDeleteAsync(BuildKey(key));
    }

    /// <summary>
    /// 检查指定的键是否存在
    /// </summary>
    /// <param name="key">要检查的 Redis 键</param>
    /// <returns>键是否存在</returns>
    public async Task<bool> ExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(BuildKey(key));
    }

    /// <summary>
    /// 增加指定键的整数值
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要增加的值，默认为 1</param>
    /// <returns>增加后的值</returns>
    public async Task<long> IncrementAsync(string key, long value = 1)
    {
        return await _db.StringIncrementAsync(BuildKey(key), value);
    }

    /// <summary>
    /// 仅当键不存在时设置值，并设置过期时间
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要设置的值</param>
    /// <param name="expiry">过期时间</param>
    /// <returns>是否设置成功（如果键已存在则返回 false）</returns>
    public async Task<bool> SetNxAsync(string key, string value, TimeSpan expiry)
    {
        return await _db.StringSetAsync(
            BuildKey(key),
            value,
            expiry,
            When.NotExists);
    }

    /// <summary>
    /// 为指定键设置过期时间
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="expiry">过期时间</param>
    /// <returns>是否设置成功</returns>
    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        return await _db.KeyExpireAsync(BuildKey(key), expiry);
    }

    public async Task<bool> LockAsync(string key, string value, TimeSpan expiry)
    {
         return await _db.StringSetAsync(BuildKey(key), value, expiry, When.NotExists);
    }
}