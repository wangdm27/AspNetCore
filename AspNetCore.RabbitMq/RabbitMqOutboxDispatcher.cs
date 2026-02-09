using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore.RabbitMq
{
    internal sealed class RabbitMqOutboxDispatcher : BackgroundService
    {
        private readonly IRabbitMqOutboxStore _store;
        private readonly IRabbitMqPublisher _publisher;
        private readonly RabbitMqOptions _options;
        private readonly ILogger<RabbitMqOutboxDispatcher> _logger;

        public RabbitMqOutboxDispatcher(
            IRabbitMqOutboxStore store,
            IRabbitMqPublisher publisher,
            RabbitMqOptions options,
            ILogger<RabbitMqOutboxDispatcher> logger)
        {
            _store = store;
            _publisher = publisher;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var messages = await _store.GetPendingAsync(_options.OutboxBatchSize, stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        await _publisher.PublishRawAsync(
                            message.Exchange,
                            message.RoutingKey,
                            message.Body,
                            message.Headers,
                            cancellationToken: stoppingToken);

                        await _store.MarkAsPublishedAsync(message.Id, DateTimeOffset.UtcNow, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Outbox message {OutboxMessageId} publish failed.", message.Id);
                        await _store.MarkAsFailedAsync(message.Id, ex.Message, stoppingToken);
                    }
                }

                await Task.Delay(_options.OutboxDispatchInterval, stoppingToken);
            }
        }
    }
}
