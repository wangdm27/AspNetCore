namespace AspNetCore.Events;

/// <summary>
/// 示例事件：用户创建。发布方与消费方共享此契约。
/// </summary>
public record UserCreatedEvent : IEvent
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
