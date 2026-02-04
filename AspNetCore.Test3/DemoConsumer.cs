using AspNetCore.RabbitMq;
using System;
using System.Collections.Generic;
using System.Text;
using static AspNetCore.Test3.DemoConsumer;

namespace AspNetCore.Test3
{
    public class DemoConsumer : RabbitMqConsumerBase<DemoMessage>
    {
        public DemoConsumer(IRabbitMqConnection connection, RabbitMqOptions options) : base(connection, options)
        {
        }

        protected override string Exchange => "demo.exchange";
        protected override string RoutingKey => "demo.key";
        protected override string Queue => "demo.queue";


        protected override Task HandleAsync(DemoMessage message, CancellationToken ct)
        {
            Console.WriteLine($"收到：{message.Text}");
            return Task.CompletedTask;
        }

        public class DemoMessage
        {
            public string Text { get; set; } = null!;
        }

    }
}
