using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AspNetCore.Redis;

/// <summary>
/// Redis 服务集合扩展类，提供 Redis 相关服务的注册方法
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// 向服务集合中添加 Redis 相关服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置 Redis 选项的委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedis(this IServiceCollection services, Action<RedisOptions> configure)
    {
        var options = new RedisOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(options.ConnectionString));
        services.AddSingleton<IRedisSerializer, JsonRedisSerializer>();
        services.AddSingleton<IRedisClient, RedisClient>();

        return services;
    }
}