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
        /// 消费者单条消息最大重试次数。超过后 nack(requeue:false) 交死信队列或丢弃，避免毒消息死循环。
        /// </summary>
        /// <remarks>默认值: 5。内存 best-effort 计数，进程重启重置。</remarks>
        public int ConsumerMaxRetryCount { get; set; } = 5;

        /// <summary>
        /// 发布确认等待超时
        /// </summary>
        /// <remarks>默认值: 10秒</remarks>
        public TimeSpan PublisherConfirmTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 校验配置合法性。非法抛 <see cref="ArgumentException"/>。由 <c>AddUnifiedRabbitMq</c> 在装配时调用。
        /// </summary>
        /// <exception cref="ArgumentException">任一规则不满足时抛出。</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                throw new ArgumentException($"{nameof(HostName)} 不能为空。", nameof(HostName));
            }

            if (Port is < 1 or > 65535)
            {
                throw new ArgumentException($"{nameof(Port)} 必须在 1..65535，当前 {Port}。", nameof(Port));
            }

            if (PrefetchCount < 1)
            {
                throw new ArgumentException($"{nameof(PrefetchCount)} 必须 >= 1（0 在 AMQP 为无限，大流量下消费者内存失控），当前 {PrefetchCount}。", nameof(PrefetchCount));
            }

            if (ChannelPoolSize < 1)
            {
                throw new ArgumentException($"{nameof(ChannelPoolSize)} 必须 >= 1，当前 {ChannelPoolSize}。", nameof(ChannelPoolSize));
            }

            if (ConsumerChannelPoolSize < 1)
            {
                throw new ArgumentException($"{nameof(ConsumerChannelPoolSize)} 必须 >= 1，当前 {ConsumerChannelPoolSize}。", nameof(ConsumerChannelPoolSize));
            }

            if (MaxRetryCount < 1)
            {
                throw new ArgumentException($"{nameof(MaxRetryCount)} 必须 >= 1，否则消息未经发布尝试即转死信，当前 {MaxRetryCount}。", nameof(MaxRetryCount));
            }

            if (ConsumerMaxRetryCount < 1)
            {
                throw new ArgumentException($"{nameof(ConsumerMaxRetryCount)} 必须 >= 1，当前 {ConsumerMaxRetryCount}。", nameof(ConsumerMaxRetryCount));
            }

            if (RetryBaseDelay < TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(RetryBaseDelay)} 不能为负，当前 {RetryBaseDelay}。", nameof(RetryBaseDelay));
            }

            if (RetryMaxDelay <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(RetryMaxDelay)} 必须为正，当前 {RetryMaxDelay}。", nameof(RetryMaxDelay));
            }

            if (PublisherConfirmTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(PublisherConfirmTimeout)} 必须为正，当前 {PublisherConfirmTimeout}。", nameof(PublisherConfirmTimeout));
            }

            if (OutboxBatchSize < 1)
            {
                throw new ArgumentException($"{nameof(OutboxBatchSize)} 必须 >= 1，当前 {OutboxBatchSize}。", nameof(OutboxBatchSize));
            }

            if (DefaultMessageTTL is { } ttl)
            {
                // x-message-ttl 为 32 位毫秒，> 24.85 天溢出
                if (ttl <= TimeSpan.Zero)
                {
                    throw new ArgumentException($"{nameof(DefaultMessageTTL)} 必须为正，当前 {ttl}。", nameof(DefaultMessageTTL));
                }

                if (ttl > TimeSpan.FromMilliseconds(int.MaxValue))
                {
                    throw new ArgumentException($"{nameof(DefaultMessageTTL)} 超过 int.MaxValue 毫秒（约 24.85 天）上限，当前 {ttl}。", nameof(DefaultMessageTTL));
                }
            }

            if (EnableDeadLetter)
            {
                if (string.IsNullOrEmpty(DeadLetterExchange))
                {
                    throw new ArgumentException($"启用 {nameof(EnableDeadLetter)} 时 {nameof(DeadLetterExchange)} 必须非空。", nameof(DeadLetterExchange));
                }

                if (string.IsNullOrEmpty(DeadLetterQueue))
                {
                    throw new ArgumentException($"启用 {nameof(EnableDeadLetter)} 时 {nameof(DeadLetterQueue)} 必须非空。", nameof(DeadLetterQueue));
                }

                if (string.IsNullOrEmpty(DeadLetterRoutingKey))
                {
                    throw new ArgumentException($"启用 {nameof(EnableDeadLetter)} 时 {nameof(DeadLetterRoutingKey)} 必须非空。", nameof(DeadLetterRoutingKey));
                }
            }
        }
    }
}
