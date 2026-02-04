using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer where T : class
    {
        private readonly IRabbitMqConnection _connection;
        private readonly RabbitMqOptions _options;


        protected abstract string Queue { get; }
        protected abstract string Exchange { get; }
        protected abstract string RoutingKey { get; }


        protected RabbitMqConsumerBase(IRabbitMqConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var conn = await _connection.GetConnectionAsync();
            var channel = await conn.CreateChannelAsync();

            // 1️ 自动声明 Exchange
            await channel.ExchangeDeclareAsync(
                exchange: Exchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null);

            // 2️ 自动声明 Queue
            await channel.QueueDeclareAsync(
                queue: Queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // 3️ 绑定 Queue 到 Exchange
            await channel.QueueBindAsync(
                queue: Queue,
                exchange: Exchange,
                routingKey: RoutingKey,
                arguments: null);

            await channel.BasicQosAsync(0, _options.PrefetchCount, false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<T>(ea.Body.Span)!;
                    await HandleAsync(msg, cancellationToken);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await channel.BasicConsumeAsync(
                queue: Queue,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        protected abstract Task HandleAsync(T message, CancellationToken ct);
    }
}
