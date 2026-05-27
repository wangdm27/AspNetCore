using System;
using System.Text.Json;

namespace AspNetCore.Redis;

/// <summary>
/// Redis JSON 序列化器实现，使用 System.Text.Json 进行对象的序列化和反序列化
/// </summary>
public class JsonRedisSerializer : IRedisSerializer
{
    /// <summary>
    /// JSON 序列化选项，使用驼峰命名策略
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 将对象序列化为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">要序列化的对象类型</typeparam>
    /// <param name="value">要序列化的对象</param>
    /// <returns>序列化后的 JSON 字符串</returns>
    public string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// 将 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">要反序列化的目标类型</typeparam>
    /// <param name="value">要反序列化的 JSON 字符串</param>
    /// <returns>反序列化后的对象，如果反序列化失败则返回 null</returns>
    public T? Deserialize<T>(string value)
        => JsonSerializer.Deserialize<T>(value, Options);
}