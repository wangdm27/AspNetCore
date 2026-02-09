using System.Collections.Concurrent;

namespace AspNetCore.RabbitMq
{
    internal sealed class InMemoryRabbitMqOutboxStore : IRabbitMqOutboxStore
    {
        private readonly ConcurrentDictionary<Guid, RabbitMqOutboxMessage> _messages = new();

        public Task AddAsync(RabbitMqOutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages[message.Id] = message;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(int takeCount, CancellationToken cancellationToken = default)
        {
            var result = _messages.Values
                .Where(x => x.PublishedAt is null)
                .OrderBy(x => x.CreatedAt)
                .Take(takeCount)
                .ToArray();

            return Task.FromResult<IReadOnlyList<RabbitMqOutboxMessage>>(result);
        }

        public Task MarkAsPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.PublishedAt = publishedAt;
            }

            return Task.CompletedTask;
        }

        public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
        {
            if (_messages.TryGetValue(messageId, out var msg))
            {
                msg.LastError = error;
                msg.RetryCount += 1;
            }

            return Task.CompletedTask;
        }
    }
}
