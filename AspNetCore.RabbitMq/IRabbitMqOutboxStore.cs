namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqOutboxStore
    {
        Task AddAsync(RabbitMqOutboxMessage message, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<RabbitMqOutboxMessage>> GetPendingAsync(int takeCount, CancellationToken cancellationToken = default);
        Task MarkAsPublishedAsync(Guid messageId, DateTimeOffset publishedAt, CancellationToken cancellationToken = default);
        Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
    }
}
