using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer
    {
        private readonly IRabbitMqConnection _connection;
        private readonly RabbitMqOptions _options;

        protected RabbitMqConsumerBase(IRabbitMqConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _options = options;
        }

        protected abstract string Queue { get; }
        protected abstract string Exchange { get; }
        protected abstract string RoutingKey { get; }

        protected abstract Task HandleAsync(T message, CancellationToken ct);

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var conn = await _connection.GetConnectionAsync();
            var channel = await conn.CreateChannelAsync();

            // 死信队列参数
            var args = new Dictionary<string, object?>();
            if (_options.EnableDeadLetter)
            {
                args["x-dead-letter-exchange"] = _options.DeadLetterExchange;
                args["x-dead-letter-routing-key"] = _options.DeadLetterQueue;
                args["x-message-ttl"] = _options.DefaultMessageTTL;


                await channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, true, false);
                await channel.QueueDeclareAsync(_options.DeadLetterQueue, true, false, false);
                await channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, _options.DeadLetterQueue);
            }


            await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, true, false);
            await channel.QueueDeclareAsync(Queue, true, false, false, args);
            await channel.QueueBindAsync(Queue, Exchange, RoutingKey);


            await channel.BasicQosAsync(0, _options.PrefetchCount, false);


            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                int retry = 0;
                while (retry <= _options.RetryCount)
                {
                    try
                    {
                        T msg = typeof(T) == typeof(string)
                        ? (T)(object)Encoding.UTF8.GetString(ea.Body.ToArray())
                        : JsonSerializer.Deserialize<T>(ea.Body.Span)!;


                        await HandleAsync(msg, cancellationToken);


                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }
                    catch
                    {
                        retry++;
                        if (retry > _options.RetryCount)
                        {
                            await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                            return;
                        }


                        await Task.Delay(1000, cancellationToken);
                    }
                }
            };

            await channel.BasicConsumeAsync(Queue, false, consumer, cancellationToken);
        }
    }
}
