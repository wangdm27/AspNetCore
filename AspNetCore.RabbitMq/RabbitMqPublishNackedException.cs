namespace AspNetCore.RabbitMq
{
    /// <summary>
    /// broker 通过 basic.nack 拒绝了发布消息时抛出。
    /// </summary>
    /// <remarks>
    /// 与发布确认超时（<see cref="TimeoutException"/>）区分：
    /// nacked 表示 broker 明确拒绝该消息（如队列满、内部错误），
    /// 超时表示在 <see cref="RabbitMqOptions.PublisherConfirmTimeout"/> 内未收到任何确认。
    /// </remarks>
    public class RabbitMqPublishNackedException : Exception
    {
        /// <summary>
        /// 被拒绝消息的发布序列号。
        /// </summary>
        public ulong PublishSequenceNumber { get; }

        public RabbitMqPublishNackedException(ulong publishSequenceNumber)
            : base($"RabbitMQ broker nacked publish sequence number {publishSequenceNumber}.")
        {
            PublishSequenceNumber = publishSequenceNumber;
        }
    }
}
