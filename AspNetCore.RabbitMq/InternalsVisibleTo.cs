using System.Runtime.CompilerServices;

// 暴露 internal 类型给测试项目，便于单元测试覆盖纯逻辑
// （如 RabbitMqTracing 的 traceparent 解析、InMemoryRabbitMqOutboxStore 的过滤排序）。
[assembly: InternalsVisibleTo("AspNetCore.RabbitMq.Tests")]
