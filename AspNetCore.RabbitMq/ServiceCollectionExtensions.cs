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
            services.AddSingleton<IRabbitMqChannelPool, RabbitMqChannelPool>();
            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

            services.TryAddSingleton<IRabbitMqOutboxStore, InMemoryRabbitMqOutboxStore>();
            services.AddSingleton<IRabbitMqOutbox, RabbitMqOutbox>();
            services.AddHostedService<RabbitMqOutboxDispatcher>();

            return services;
        }
    }
}
