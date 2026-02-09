using AspNetCore.RabbitMq;
using System;
using System.Collections.Generic;
using System.Text;
using static AspNetCore.Test3.DemoConsumer;

namespace AspNetCore.Test3
{
    public class DemoConsumer : RabbitMqConsumerBase<string>
    {
        public DemoConsumer(IRabbitMqConnection conn, RabbitMqOptions opts) : base(conn, opts) { }

        protected override string Queue => "demo.queue";
        protected override string Exchange => "demo.exchange";
        protected override string RoutingKey => "demo.key";

        protected override Task HandleAsync(string message, CancellationToken ct)
        {
            Console.WriteLine($"Received message: {message}");
            return Task.CompletedTask;
        }

    }
}
