using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();


            return services;
        }
    }
}
