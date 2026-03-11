using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// RabbitMQ消费者基类
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <remarks>
    /// 提供了RabbitMQ消费者的基本实现，包括队列声明、绑定和消息处理
    /// 子类需要实现队列、交换机和路由键的配置，以及消息处理逻辑
    /// </remarks>
    public abstract class RabbitMqConsumerBase<T> : IRabbitMqConsumer where T : class
    {
        /// <summary>
        /// RabbitMQ连接实例
        /// </summary>
        private readonly IRabbitMqConnection _connection;

        /// <summary>
        /// RabbitMQ配置选项
        /// </summary>
        private readonly RabbitMqOptions _options;

        /// <summary>
        /// 队列名称
        /// </summary>
        /// <remarks>
        /// 子类必须实现此属性，返回要消费的队列名称
        /// </remarks>
        protected abstract string Queue { get; }

        /// <summary>
        /// 交换机名称
        /// </summary>
        /// <remarks>
        /// 子类必须实现此属性，返回要绑定的交换机名称
        /// </remarks>
        protected abstract string Exchange { get; }

        /// <summary>
        /// 路由键
        /// </summary>
        /// <remarks>
        /// 子类必须实现此属性，返回队列绑定的路由键
        /// </remarks>
        protected abstract string RoutingKey { get; }

        /// <summary>
        /// 初始化消费者基类
        /// </summary>
        /// <param name="connection">RabbitMQ连接实例</param>
        /// <param name="options">RabbitMQ配置选项</param>
        protected RabbitMqConsumerBase(IRabbitMqConnection connection, RabbitMqOptions options)
        {
            _connection = connection;
            _options = options;
        }

        /// <summary>
        /// 启动消费者
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步启动操作的任务</returns>
        /// <remarks>
        /// 此方法执行以下操作：
        /// 1. 获取RabbitMQ连接和通道
        /// 2. 声明交换机
        /// 3. 声明队列
        /// 4. 绑定队列到交换机
        /// 5. 设置QoS
        /// 6. 创建消费者
        /// 7. 注册消息接收事件处理
        /// 8. 开始消费消息
        /// </remarks>
        /// <exception cref="RabbitMQ.Client.Exceptions.BrokerUnreachableException">当无法连接到RabbitMQ服务器时抛出</exception>
        /// <exception cref="RabbitMQ.Client.Exceptions.AlreadyClosedException">当RabbitMQ连接已关闭时抛出</exception>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var conn = await _connection.GetConnectionAsync();
            var channel = await conn.CreateChannelAsync();

            // 1️ 自动声明 Exchange
            await channel.ExchangeDeclareAsync(
                exchange: Exchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null);

            // 2️ 自动声明 Queue
            await channel.QueueDeclareAsync(
                queue: Queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // 3️ 绑定 Queue 到 Exchange
            await channel.QueueBindAsync(
                queue: Queue,
                exchange: Exchange,
                routingKey: RoutingKey,
                arguments: null);

            // 设置QoS，控制消息预取数量
            await channel.BasicQosAsync(0, _options.PrefetchCount, false);

            // 创建异步事件消费者
            var consumer = new AsyncEventingBasicConsumer(channel);

            // 注册消息接收事件处理
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    // 反序列化消息
                    var msg = JsonSerializer.Deserialize<T>(ea.Body.Span)!;
                    // 处理消息
                    await HandleAsync(msg, cancellationToken);
                    // 确认消息处理成功
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch
                {
                    // 消息处理失败，重新入队
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // 开始消费消息
            await channel.BasicConsumeAsync(
                queue: Queue,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>表示异步处理操作的任务</returns>
        /// <remarks>
        /// 子类必须实现此方法，处理具体的消息逻辑
        /// </remarks>
        protected abstract Task HandleAsync(T message, CancellationToken ct);
    }
}
