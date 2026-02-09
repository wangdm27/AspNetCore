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

        public bool EnableDeadLetter { get; set; } = true;
        public string DeadLetterExchange { get; set; } = "dead.letter.exchange";
        public string DeadLetterQueue { get; set; } = "dead.letter.queue";
        public int DefaultMessageTTL { get; set; } = 60000; // ms

    }
}
