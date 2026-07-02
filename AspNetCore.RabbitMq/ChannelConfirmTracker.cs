using System.Collections.Concurrent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AspNetCore.RabbitMq;

/// <summary>
/// 发布确认结果。
/// </summary>
internal enum PublishConfirmResult
{
    /// <summary>broker 通过 basic.ack 确认。</summary>
    Confirmed,

    /// <summary>broker 通过 basic.nack 拒绝。</summary>
    Nacked,

    /// <summary>超出等待超时未收到确认。</summary>
    TimedOut
}

/// <summary>
/// 通道级发布确认追踪器
/// </summary>
/// <remarks>
/// 每个 IChannel 绑定一个追踪器，订阅 BasicAcksAsync / BasicNacksAsync，
/// 维护 deliveryTag → TaskCompletionSource 映射，供发布者在发布后等待 broker 确认。
/// 通道由池长期持有，追踪器生命周期与通道一致。
/// </remarks>
internal sealed class ChannelConfirmTracker : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<PublishConfirmResult>> _pending = new();

    public ChannelConfirmTracker(IChannel channel)
    {
        _channel = channel;
        _channel.BasicAcksAsync += OnAcksAsync;
        _channel.BasicNacksAsync += OnNacksAsync;
    }

    /// <summary>
    /// 注册一个待确认序列号，返回可等待的 TaskCompletionSource。
    /// </summary>
    public TaskCompletionSource<PublishConfirmResult> Register(ulong seq)
    {
        var tcs = new TaskCompletionSource<PublishConfirmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = tcs;
        return tcs;
    }

    /// <summary>
    /// 等待指定序列号的 broker 确认。超时返回 TimedOut，取消向上抛。
    /// </summary>
    public async Task<PublishConfirmResult> WaitAsync(ulong seq, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(seq, out var tcs))
        {
            return PublishConfirmResult.Confirmed;
        }

        try
        {
            return await tcs.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return PublishConfirmResult.TimedOut;
        }
    }

    /// <summary>
    /// 移除一个序列号（无论是否确认完成）。
    /// </summary>
    public void Remove(ulong seq) => _pending.TryRemove(seq, out _);

    private Task OnAcksAsync(object? sender, BasicAckEventArgs e)
    {
        if (e.Multiple)
        {
            foreach (var kvp in _pending)
            {
                if (kvp.Key <= e.DeliveryTag)
                {
                    kvp.Value.TrySetResult(PublishConfirmResult.Confirmed);
                }
            }
        }
        else if (_pending.TryGetValue(e.DeliveryTag, out var tcs))
        {
            tcs.TrySetResult(PublishConfirmResult.Confirmed);
        }

        return Task.CompletedTask;
    }

    private Task OnNacksAsync(object? sender, BasicNackEventArgs e)
    {
        if (e.Multiple)
        {
            foreach (var kvp in _pending)
            {
                if (kvp.Key <= e.DeliveryTag)
                {
                    kvp.Value.TrySetResult(PublishConfirmResult.Nacked);
                }
            }
        }
        else if (_pending.TryGetValue(e.DeliveryTag, out var tcs))
        {
            tcs.TrySetResult(PublishConfirmResult.Nacked);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _channel.BasicAcksAsync -= OnAcksAsync;
        _channel.BasicNacksAsync -= OnNacksAsync;

        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetException(new ObjectDisposedException(nameof(ChannelConfirmTracker)));
        }

        _pending.Clear();
        return ValueTask.CompletedTask;
    }
}
