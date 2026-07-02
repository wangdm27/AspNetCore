namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ 配置选项类
    /// </summary>
    /// <remarks>
    /// 包含连接 RabbitMQ 服务器的所有配置参数，以及通道池和发件箱的相关设置
    /// </remarks>
    public class RabbitMqOptions
    {
        /// <summary>
        /// RabbitMQ 服务器主机名
        /// </summary>
        /// <remarks>默认值: localhost</remarks>
        public string HostName { get; set; } = "localhost";

        /// <summary>
        /// RabbitMQ 服务器端口
        /// </summary>
        /// <remarks>默认值: 5672 (AMQP 协议默认端口)</remarks>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// RabbitMQ 连接用户名
        /// </summary>
        /// <remarks>默认值: guest</remarks>
        public string UserName { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ 连接密码
        /// </summary>
        /// <remarks>默认值: guest</remarks>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// RabbitMQ 虚拟主机
        /// </summary>
        /// <remarks>默认值: /</remarks>
        public string VirtualHost { get; set; } = "/";

        /// <summary>
        /// 消息预取计数
        /// </summary>
        /// <remarks>
        /// 控制每次从队列中获取的消息数量，用于流控
        /// 默认值: 10
        /// </remarks>
        public ushort PrefetchCount { get; set; } = 10;

        /// <summary>
        /// 是否启用自动连接恢复
        /// </summary>
        /// <remarks>默认值: true</remarks>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用拓扑恢复
        /// </summary>
        /// <remarks>
        /// 控制是否在连接恢复时重新声明交换机、队列和绑定
        /// 默认值: true
        /// </remarks>
        public bool TopologyRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// 网络恢复间隔
        /// </summary>
        /// <remarks>默认值: 10秒</remarks>
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 通道池大小
        /// </summary>
        /// <remarks>
        /// 控制可同时使用的最大通道数
        /// 默认值: 16
        /// </remarks>
        public int ChannelPoolSize { get; set; } = 16;

        /// <summary>
        /// 发件箱调度间隔
        /// </summary>
        /// <remarks>
        /// 控制发件箱消息的处理频率
        /// 默认值: 3秒
        /// </remarks>
        public TimeSpan OutboxDispatchInterval { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 发件箱批处理大小
        /// </summary>
        /// <remarks>
        /// 控制每次处理的发件箱消息数量
        /// 默认值: 100
        /// </remarks>
        public int OutboxBatchSize { get; set; } = 100;

        /// <summary>
        /// 是否启用死信队列
        /// </summary>
        /// <remarks>默认值: false</remarks>
        public bool EnableDeadLetter { get; set; } = false;

        /// <summary>
        /// 死信交换机名称
        /// </summary>
        /// <remarks>默认值: string.Empty</remarks>
        public string DeadLetterExchange { get; set; } = string.Empty;

        /// <summary>
        /// 死信路由键（修正原误用队列名作路由键的 bug）
        /// </summary>
        /// <remarks>默认值: string.Empty</remarks>
        public string DeadLetterRoutingKey { get; set; } = string.Empty;

        /// <summary>
        /// 死信队列名称
        /// </summary>
        /// <remarks>默认值: string.Empty</remarks>
        public string DeadLetterQueue { get; set; } = string.Empty;

        /// <summary>
        /// 消息默认存活时间（主队列 x-message-ttl），null 表示不设置
        /// </summary>
        /// <remarks>默认值: null</remarks>
        public TimeSpan? DefaultMessageTTL { get; set; } = null;

        /// <summary>
        /// Outbox 最大重试次数
        /// </summary>
        /// <remarks>默认值: 5</remarks>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>
        /// Outbox 重试退避基数
        /// </summary>
        /// <remarks>默认值: 5秒</remarks>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Outbox 重试退避封顶
        /// </summary>
        /// <remarks>默认值: 5分钟</remarks>
        public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 消费者通道池大小
        /// </summary>
        /// <remarks>默认值: 16</remarks>
        public int ConsumerChannelPoolSize { get; set; } = 16;

        /// <summary>
        /// 发布确认等待超时
        /// </summary>
        /// <remarks>默认值: 10秒</remarks>
        public TimeSpan PublisherConfirmTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}
