using System;

namespace AspNetCore.Redis;

/// <summary>
/// Redis 序列化器接口，用于在 Redis 操作中序列化和反序列化对象
/// </summary>
public interface IRedisSerializer
{
    /// <summary>
    /// 将对象序列化为字符串
    /// </summary>
    /// <typeparam name="T">要序列化的对象类型</typeparam>
    /// <param name="value">要序列化的对象</param>
    /// <returns>序列化后的字符串</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// 将字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="value">要反序列化的字符串</param>
    /// <returns>反序列化后的对象；反序列化失败时抛出 <see cref="System.Text.Json.JsonException"/>（不返回 null）</returns>
    T? Deserialize<T>(string value);
}