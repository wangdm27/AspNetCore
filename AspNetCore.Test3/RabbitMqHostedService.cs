using Microsoft.Extensions.Hosting;

namespace AspNetCore.RabbitMq
{
    public class RabbitMqHostedService : BackgroundService
    {
        private readonly IEnumerable<IRabbitMqConsumer> _consumers;


        public RabbitMqHostedService(IEnumerable<IRabbitMqConsumer> consumers)
        {
            _consumers = consumers;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            foreach (var c in _consumers)
                await c.StartAsync(stoppingToken);


            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
