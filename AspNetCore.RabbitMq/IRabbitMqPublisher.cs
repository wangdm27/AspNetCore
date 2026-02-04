using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqPublisher
    {
        ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null);
    }
}
