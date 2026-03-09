namespace AspNetCore.RabbitMq
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;                 // RabbitMQ 默认端口
        public string VirtualHost { get; set; } = "/";       // 默认 vhost
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";

        public ushort PrefetchCount { get; set; } = 10;
        public int RetryCount { get; set; } = 3;
        public bool AutoReconnect { get; set; } = true;
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
        public ushort ConsumerConcurrency { get; set; } = 10; // 异步消费者并发

        public ushort PrefetchCount { get; set; } = 10;

        public bool AutomaticRecoveryEnabled { get; set; } = true;
        public bool TopologyRecoveryEnabled { get; set; } = true;
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);

        public int ChannelPoolSize { get; set; } = 16;

        public TimeSpan OutboxDispatchInterval { get; set; } = TimeSpan.FromSeconds(3);
        public int OutboxBatchSize { get; set; } = 100;
    }
}
