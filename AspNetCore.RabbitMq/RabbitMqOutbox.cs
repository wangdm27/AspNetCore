using System.Text.Json;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqOutbox : IRabbitMqOutbox
    {
        private readonly IRabbitMqOutboxStore _store;

        public RabbitMqOutbox(IRabbitMqOutboxStore store)
        {
            _store = store;
        }

        public async ValueTask EnqueueAsync<T>(
            string exchange,
            string routingKey,
            T message,
            Action<IBasicProperties>? props = null,
            CancellationToken cancellationToken = default)
        {
            var properties = new BasicProperties();
            props?.Invoke(properties);

            var outboxMessage = new RabbitMqOutboxMessage
            {
                Exchange = exchange,
                RoutingKey = routingKey,
                Body = JsonSerializer.SerializeToUtf8Bytes(message),
                Headers = properties.Headers?.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value)
                    ?? new Dictionary<string, object?>()
            };

            await _store.AddAsync(outboxMessage, cancellationToken);
        }
    }
}
