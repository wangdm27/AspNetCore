using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq
{
    public sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqChannelPool _channelPool;

        public RabbitMqPublisher(IRabbitMqConnection connection)
        {
            _channelPool = channelPool;
        }

        public async Task PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            bool confirm = true,
            int? delayMs = null,
            CancellationToken cancellationToken = default)
        {
            var conn = await _connection.GetConnectionAsync();
            var channelOptions = confirm
                ? new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true)
                : null;
            await using var channel = await conn.CreateChannelAsync(channelOptions);

            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, autoDelete: false);

            byte[] body = message is string s ? Encoding.UTF8.GetBytes(s) : JsonSerializer.SerializeToUtf8Bytes(message);

            var props = new BasicProperties { Persistent = true };

            if (delayMs.HasValue)
            {
                props.Headers = new Dictionary<string, object?> { ["x-delay"] = delayMs.Value };
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

            if (!confirm)
            {
                await channel.BasicPublishAsync(exchange, routingKey, false, props, body, cancellationToken);
                return;
            }

            using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await channel.BasicPublishAsync(exchange, routingKey, false, props, body, publishCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("RabbitMQ publish confirm timed out after 5 seconds.");
            }
        }
    }
}
