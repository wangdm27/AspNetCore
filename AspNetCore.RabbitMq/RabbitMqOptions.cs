namespace AspNetCore.RabbitMq
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";

        public ushort PrefetchCount { get; set; } = 10;

        public bool AutomaticRecoveryEnabled { get; set; } = true;
        public bool TopologyRecoveryEnabled { get; set; } = true;
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);

        public int ChannelPoolSize { get; set; } = 16;

        public TimeSpan OutboxDispatchInterval { get; set; } = TimeSpan.FromSeconds(3);
        public int OutboxBatchSize { get; set; } = 100;
    }
}
