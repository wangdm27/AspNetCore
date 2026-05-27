﻿namespace AspNetCore.Redis;

/// <summary>
/// Redis 键生成器类，用于生成标准化的 Redis 键
/// </summary>
public class RedisKey
{
    /// <summary>
    /// 构建 Redis 键
    /// </summary>
    /// <param name="prefix">键前缀</param>
    /// <param name="key">键值</param>
    /// <returns>构建后的 Redis 键</returns>
    public static string Build(string prefix, string key)
        => $"{prefix}{key}";

    /// <summary>
    /// 生成用户相关的 Redis 键
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户相关的 Redis 键</returns>
    public static string User(string userId)
        => $"user:{userId}";

    /// <summary>
    /// 生成订单相关的 Redis 键
    /// </summary>
    /// <param name="orderId">订单 ID</param>
    /// <returns>订单相关的 Redis 键</returns>
    public static string Order(string orderId)
        => $"order:{orderId}";
}