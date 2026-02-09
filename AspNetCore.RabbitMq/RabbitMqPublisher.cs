using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    public sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqConnection _connection;

        public RabbitMqPublisher(IRabbitMqConnection connection)
        {
            _connection = connection;
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
            await using var channel = await conn.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, autoDelete: false);

            byte[] body = message is string s ? Encoding.UTF8.GetBytes(s) : JsonSerializer.SerializeToUtf8Bytes(message);

            var props = new BasicProperties { Persistent = true };

            if (delayMs.HasValue)
            {
                props.Headers = new Dictionary<string, object?> { ["x-delay"] = delayMs.Value };
            }


            if (!confirm)
            {
                await channel.BasicPublishAsync(exchange, routingKey, false, props, body);
                return;
            }


            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);


            async Task AckHandler(object sender, BasicAckEventArgs ea)
            {
                tcs.TrySetResult(true);
                await Task.CompletedTask;
            }


            async Task NackHandler(object sender, BasicNackEventArgs ea)
            {
                tcs.TrySetResult(false);
                await Task.CompletedTask;
            }


            channel.BasicAcksAsync += AckHandler;
            channel.BasicNacksAsync += NackHandler;


            try
            {
                await channel.BasicPublishAsync(exchange, routingKey, false, props, body);


                var confirmed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));


                if (!confirmed)
                    throw new Exception("RabbitMQ publish nack received");
            }
            finally
            {
                channel.BasicAcksAsync -= AckHandler;
                channel.BasicNacksAsync -= NackHandler;
            }
        }
    }
}
