namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ发件箱消息类
    /// </summary>
    /// <remarks>
    /// 表示存储在发件箱中的待发布消息
    /// 包含消息的基本信息和状态信息
    /// 是发件箱模式的核心数据结构
    /// </remarks>
    public sealed class RabbitMqOutboxMessage
    {
        /// <summary>
        /// 消息唯一标识
        /// </summary>
        /// <remarks>
        /// 自动生成的GUID，用于唯一标识消息
        /// </remarks>
        public Guid Id { get; init; } = Guid.NewGuid();
        
        /// <summary>
        /// 交换机名称
        /// </summary>
        /// <remarks>
        /// 消息要发布到的RabbitMQ交换机
        /// </remarks>
        public required string Exchange { get; init; }
        
        /// <summary>
        /// 路由键
        /// </summary>
        /// <remarks>
        /// 用于消息路由的键值
        /// </remarks>
        public required string RoutingKey { get; init; }
        
        /// <summary>
        /// 消息体
        /// </summary>
        /// <remarks>
        /// 消息的二进制内容，通常是序列化后的对象
        /// </remarks>
        public required byte[] Body { get; init; }
        
        /// <summary>
        /// 消息头
        /// </summary>
        /// <remarks>
        /// 消息的附加属性，默认为空字典
        /// </remarks>
        public Dictionary<string, object?> Headers { get; init; } = new();

        /// <summary>
        /// 消息内容类型（对应 <c>IBasicProperties.ContentType</c>），发布时重建。null 表示未设置。
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// 关联标识（对应 <c>IBasicProperties.CorrelationId</c>），发布时重建。null 表示未设置。
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// 消息标识（对应 <c>IBasicProperties.MessageId</c>），发布时重建；消费者可用作重试计数 key。null 表示未设置。
        /// </summary>
        public string? MessageId { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        /// <remarks>
        /// 消息创建的时间戳，使用UTC时间
        /// </remarks>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        
        /// <summary>
        /// 发布时间
        /// </summary>
        /// <remarks>
        /// 消息成功发布到RabbitMQ的时间戳，未发布时为null
        /// </remarks>
        public DateTimeOffset? PublishedAt { get; set; }
        
        /// <summary>
        /// 重试次数
        /// </summary>
        /// <remarks>
        /// 消息发布失败的重试次数
        /// </remarks>
        public int RetryCount { get; set; }
        
        /// <summary>
        /// 最后错误信息
        /// </summary>
        /// <remarks>
        /// 上次发布失败的错误信息，未失败时为null
        /// </remarks>
        public string? LastError { get; set; }

        /// <summary>
        /// 下次允许重试时间，null 表示立即可重试
        /// </summary>
        public DateTimeOffset? NextAttemptAt { get; set; }

        /// <summary>
        /// 是否已转死信
        /// </summary>
        public bool DeadLettered { get; set; }
    }
}