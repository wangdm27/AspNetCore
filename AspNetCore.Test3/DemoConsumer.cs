using AspNetCore.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using System;
using static AspNetCore.Test3.DemoConsumer;

namespace AspNetCore.Test3
{
    public class DemoConsumer : RabbitMqConsumerBase<DemoMessage>
    {
        public DemoConsumer([FromKeyedServices("consumer")] IRabbitMqChannelPool pool, RabbitMqOptions opts) : base(pool, opts) { }

        protected override string Queue => "demo.queue";
        protected override string Exchange => "demo.exchange";
        protected override string RoutingKey => "demo.key";

        protected override Task HandleAsync(DemoMessage message, CancellationToken ct)
        {
            Console.WriteLine($"Received message: {message.Text}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Demo 消息实体（由 Program 发送、DemoConsumer 消费）。
    /// </summary>
    public record DemoMessage
    {
        public string? Text { get; set; }
    }
}
