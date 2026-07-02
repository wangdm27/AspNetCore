# 05 · Test 测试项目

> 三个控制台项目，均为 `net10.0`、`OutputType=Exe`。
> **注**：这些项目为学习/验证用途，**非自动化测试**（无 xUnit/NUnit 等测试框架），需 RabbitMQ 实例运行才能验证。

## 1. 概览

| 项目 | 依赖 | 用途 |
| --- | --- | --- |
| `AspNetCore.Test` | `RabbitMQ.Client`（直接引用） | RabbitMQ 官方教程示例：发布者（direct 交换机） |
| `AspNetCore.Test2` | `RabbitMQ.Client`（直接引用） | RabbitMQ 官方教程示例：消费者（topic 交换机） |
| `AspNetCore.Test3` | `AspNetCore.RabbitMq` + `Microsoft.Extensions.Hosting` | `AspNetCore.RabbitMq` 库集成示例 |

> `Test` / `Test2` 直接使用 `RabbitMQ.Client`，**不经过** `AspNetCore.RabbitMq` 封装。
> `Test3` 是唯一验证 `AspNetCore.RabbitMq` 库的项目。

## 2. AspNetCore.Test — 原生发布者

`AspNetCore.Test/Program.cs`

- 连接 `localhost`，声明 `direct_logs` 交换机（`ExchangeType.Direct`）。
- 默认路由键 `info`，发布 `Hello World!` 消息。
- 文件内**大量注释代码**：Simple Queue、Work Queue、Fanout Pub/Sub 示例（保留作教程对照）。
- `args` 被硬编码覆盖为 `["info","warning","error"]`（`Program.cs:6-8`），仅取 `args[0]` 作路由键。

> **TODO**：`args[0/1/2]` 直接赋值，若 `Main` 入参 `args` 为只读或长度不足，运行时可能抛 `IndexOutOfRangeException`。属示例代码，未做防护。

## 3. AspNetCore.Test2 — 原生消费者

`AspNetCore.Test2/Program.cs`

- 默认绑定键 `["kern.*","*.critical"]`（`Program.cs:7`）。
- 声明 `topic_logs` 交换机（`ExchangeType.Topic`），创建匿名队列，按 `args` 绑定路由键。
- `AsyncEventingBasicConsumer` 消费，打印路由键与消息。
- 同样保留 Simple/Work/Fanout/Direct 的注释示例。

## 4. AspNetCore.Test3 — 库集成示例

`AspNetCore.Test3/Program.cs`

使用 `Microsoft.Extensions.Hosting` + `AddUnifiedRabbitMq`：

```
opt.HostName = "localhost"; opt.UserName = "guest"; opt.Password = "guest";
opt.ChannelPoolSize = 8;
opt.OutboxDispatchInterval = 2s;
opt.OutboxBatchSize = 50;
```

流程（`Program.cs:30-55`）：

1. 注册 `DemoConsumer` 为 `IRabbitMqConsumer`。
2. `host.StartAsync()`（启动 `RabbitMqOutboxDispatcher` 后台服务）。
3. 手动 `consumer.StartAsync()` 启动消费者。
4. 直发一条消息：`publisher.PublishAsync("demo.exchange","demo.key", DemoMessage)`。
5. Outbox 入箱一条：`outbox.EnqueueAsync(...)`（后台调度器异步投递）。
6. `Task.Delay(5s)` 等待分发，`host.StopAsync()`。

### `DemoConsumer`（`DemoConsumer.cs`）

继承 `RabbitMqConsumerBase<string>`：

- `Queue = "demo.queue"`，`Exchange = "demo.exchange"`，`RoutingKey = "demo.key"`。
- `HandleAsync` 打印消息。

> **TODO（编译）**：`Program.cs` 与 `DemoConsumer.cs` 引用 `DemoMessage` 类型，但项目中**未见 `DemoMessage` 定义**（已读全 3 个文件）。`DemoMessage` 缺失将导致 `Test3` 无法编译。
> **TODO（编译）**：`DemoConsumer` 继承 `RabbitMqConsumerBase<T>`，而 `RabbitMqConsumerBase.StartAsync` 引用 `RabbitMqOptions` 上不存在的死信属性（见 [02 §3/§11](./02-RabbitMq-消息队列库.md)）。即便补全 `DemoMessage`，`Test3` 仍受 `AspNetCore.RabbitMq` 库编译错误阻断。

### `RabbitMqHostedService`（`Test3/RabbitMqHostedService.cs`）

- 继承 `BackgroundService`，启动时遍历 `IEnumerable<IRabbitMqConsumer>` 调 `StartAsync`，随后 `Task.Delay(Timeout.Infinite)` 保持运行。
- **命名空间错位**：定义在 `Test3` 项目，却声明于 `AspNetCore.RabbitMq` 命名空间（`RabbitMqHostedService.cs:5`）。
- **未在 `Program.cs` 注册**——`Test3` 手动启动消费者，该类属遗留/未完成代码。TODO：归属与去留待确认。

## 5. 运行前提

- 本地 RabbitMQ 实例（`localhost:5672`，`guest/guest`）。
- `Test`/`Test2`：需对应交换机/队列前置声明或自行声明（示例内含声明）。
- `Test3`：依赖 `AspNetCore.RabbitMq` 库可编译（当前不可，见 TODO）。

## 6. 待确认事项（TODO）

- **TODO（编译阻断）**：`Test3` 缺 `DemoMessage` 类型定义。
- **TODO（编译阻断）**：`Test3` 受 `AspNetCore.RabbitMq` 库编译错误阻断（死信属性缺失、`IRabbitMqPublisher`/实现签名不一致）。
- **TODO**：`Test3/RabbitMqHostedService` 命名空间错位且未注册，疑似遗留代码。
- **TODO**：`Test`/`Test2` 大量注释示例代码，是否为有意保留的教学对照待确认。
- **TODO**：三个项目均非自动化测试套件，是否计划引入测试框架（xUnit 等）做单元/集成测试待确认。
