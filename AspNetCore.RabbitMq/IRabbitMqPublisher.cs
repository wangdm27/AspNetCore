using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqPublisher
    {
        ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);

        ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default);
    }
}
