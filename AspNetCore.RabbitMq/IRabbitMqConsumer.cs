namespace AspNetCore.RabbitMq
{
    public interface IRabbitMqConsumer
    {
        Task StartAsync(CancellationToken cancellationToken = default);
    }
}
