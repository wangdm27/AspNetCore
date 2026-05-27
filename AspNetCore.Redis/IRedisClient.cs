using System;

namespace AspNetCore.Redis;

/// <summary>
/// Redis 客户端接口，提供与 Redis 服务器交互的核心方法
/// </summary>
public interface IRedisClient
{
    /// <summary>
    /// 设置指定键的值，并可选设置过期时间
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要设置的值</param>
    /// <param name="expiry">过期时间，为 null 表示永不过期</param>
    /// <returns>是否设置成功</returns>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null);

    /// <summary>
    /// 获取指定键的值
    /// </summary>
    /// <typeparam name="T">返回值的类型</typeparam>
    /// <param name="key">Redis 键</param>
    /// <returns>键对应的值，如果键不存在则返回 null</returns>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// 删除指定的键
    /// </summary>
    /// <param name="key">要删除的 Redis 键</param>
    /// <returns>是否删除成功</returns>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// 检查指定的键是否存在
    /// </summary>
    /// <param name="key">要检查的 Redis 键</param>
    /// <returns>键是否存在</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 增加指定键的整数值
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要增加的值，默认为 1</param>
    /// <returns>增加后的值</returns>
    Task<long> IncrementAsync(string key, long value = 1);

    /// <summary>
    /// 仅当键不存在时设置值，并设置过期时间
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要设置的值</param>
    /// <param name="expiry">过期时间</param>
    /// <returns>是否设置成功（如果键已存在则返回 false）</returns>
    Task<bool> SetNxAsync(string key, string value, TimeSpan expiry);

    /// <summary>
    /// 为指定键设置过期时间
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="expiry">过期时间</param>
    /// <returns>是否设置成功</returns>
    Task<bool> ExpireAsync(string key, TimeSpan expiry);

    /// <summary>
    /// 尝试获取指定键的值，如果键不存在则设置为指定值并返回 true，否则返回 false
    /// 用于实现分布式锁
    /// </summary>
    /// <param name="key">Redis 键</param>
    /// <param name="value">要设置的值</param>
    /// <param name="expiry">过期时间</param>
    /// <returns>是否成功获取锁</returns>
    Task<bool> LockAsync(string key, string value, TimeSpan expiry);
}