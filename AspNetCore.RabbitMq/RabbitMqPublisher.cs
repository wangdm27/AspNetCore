using RabbitMQ.Client;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqPublisher : IRabbitMqPublisher
    {
        private readonly IRabbitMqConnection _connection;


        public RabbitMqPublisher(IRabbitMqConnection connection)
        {
            _connection = connection;
        }


        public async ValueTask PublishAsync<T>(string exchange, string routingKey, T message, Action<IBasicProperties>? props = null)
        {
            var conn = await _connection.GetConnectionAsync();


            // 7.x：CreateChannelAsync 取代 CreateModel
            await using var channel = await conn.CreateChannelAsync();


            var body = JsonSerializer.SerializeToUtf8Bytes(message);


            var properties = new BasicProperties
            {
                Persistent = true
            };


            props?.Invoke(properties);


            await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
        }
    }
}
