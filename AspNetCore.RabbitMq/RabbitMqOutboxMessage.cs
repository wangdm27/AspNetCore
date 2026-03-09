namespace AspNetCore.RabbitMq
{
    public sealed class RabbitMqOutboxMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public required string Exchange { get; init; }
        public required string RoutingKey { get; init; }
        public required byte[] Body { get; init; }
        public Dictionary<string, object?> Headers { get; init; } = new();
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? PublishedAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
    }
}
