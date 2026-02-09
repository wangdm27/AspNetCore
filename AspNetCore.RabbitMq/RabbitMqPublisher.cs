using System.Text.Json;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqChannelPool _channelPool;

        public RabbitMqPublisher(IRabbitMqChannelPool channelPool)
        {
            _channelPool = channelPool;
        }

        public ValueTask PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            return PublishRawAsync(exchange, routingKey, body, null, props, cancellationToken);
        }

        public async ValueTask PublishRawAsync(
            string exchange,
            string routingKey,
            ReadOnlyMemory<byte> body,
            IDictionary<string, object?>? headers = null,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            await using var lease = await _channelPool.RentAsync(cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                Headers = headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
            };

            props?.Invoke(properties);

            await lease.Channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
