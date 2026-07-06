using AspNetCore.Events;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    /// <summary>
    /// 示例：事件发布端点。演示 <see cref="IEventBus"/> 的 PublishAsync（直发）与 EnqueueAsync（Outbox）。
    /// 匿名可访问，便于端到端验证事件链路（Api 发布 → RabbitMQ → EventDriven 消费）。
    /// </summary>
    [ApiController]
    [Route("api/demo")]
    public class DemoEventsController : ControllerBase
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger<DemoEventsController> _logger;

        public DemoEventsController(IEventBus eventBus, ILogger<DemoEventsController> logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        /// <summary>
        /// 直发：立即投递到 broker（经发布确认 confirm）。
        /// POST /api/demo/publish-user-created
        /// </summary>
        [HttpPost("publish-user-created")]
        public async Task<IActionResult> PublishUserCreated(CancellationToken ct)
        {
            var evt = new UserCreatedEvent
            {
                UserId = Guid.NewGuid(),
                UserName = $"user_{Random.Shared.Next(1000, 9999)}",
                Email = $"user{Random.Shared.Next(1000, 9999)}@example.com"
            };

            await _eventBus.PublishAsync(evt, ct);
            _logger.LogInformation("Published UserCreatedEvent (direct): {UserId} {UserName}", evt.UserId, evt.UserName);

            return Ok(new { evt.UserId, evt.UserName, evt.Email, Mode = "Direct" });
        }

        /// <summary>
        /// Outbox：入发件箱，由后台调度器可靠投递（含重试退避 + 死信兜底）。
        /// POST /api/demo/enqueue-user-created
        /// </summary>
        [HttpPost("enqueue-user-created")]
        public async Task<IActionResult> EnqueueUserCreated(CancellationToken ct)
        {
            var evt = new UserCreatedEvent
            {
                UserId = Guid.NewGuid(),
                UserName = $"user_{Random.Shared.Next(1000, 9999)}",
                Email = $"user{Random.Shared.Next(1000, 9999)}@example.com"
            };

            await _eventBus.EnqueueAsync(evt, ct);
            _logger.LogInformation("Enqueued UserCreatedEvent (outbox): {UserId} {UserName}", evt.UserId, evt.UserName);

            return Ok(new { evt.UserId, evt.UserName, evt.Email, Mode = "Outbox" });
        }
    }
}
