using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetCore.RabbitMq
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddUnifiedRabbitMq(
            this IServiceCollection services,
            Action<RabbitMqOptions> configure)
        {
            var options = new RabbitMqOptions();
            configure(options);

            services.AddSingleton(options);
            services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();

            // 发布者通道池
            services.AddKeyedSingleton<IRabbitMqChannelPool>("publisher", (sp, _) =>
                new RabbitMqChannelPool(
                    sp.GetRequiredService<IRabbitMqConnection>(),
                    options.ChannelPoolSize));

            // 消费者通道池
            services.AddKeyedSingleton<IRabbitMqChannelPool>("consumer", (sp, _) =>
                new RabbitMqChannelPool(
                    sp.GetRequiredService<IRabbitMqConnection>(),
                    options.ConsumerChannelPoolSize));

            // 发布者注入发布者池与配置
            services.AddSingleton<IRabbitMqPublisher>(sp =>
                new RabbitMqPublisher(
                    sp.GetRequiredKeyedService<IRabbitMqChannelPool>("publisher"),
                    options));

            services.TryAddSingleton<IRabbitMqOutboxStore, InMemoryRabbitMqOutboxStore>();
            services.AddSingleton<IRabbitMqOutbox, RabbitMqOutbox>();
            services.AddHostedService<RabbitMqOutboxDispatcher>();

            return services;
        }
    }
}
