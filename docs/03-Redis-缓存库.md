# 03 · Redis 缓存库

> 项目：`AspNetCore.Redis/AspNetCore.Redis.csproj`
> 命名空间：`AspNetCore.Redis`（全部类型统一在该命名空间）
> 依赖：`StackExchange.Redis 2.12.14`

## 1. 模块职责

封装 `StackExchange.Redis`，提供：

- 键前缀统一管理。
- 对象 JSON 序列化/反序列化。
- 常用字符串操作（Get/Set/Remove/Exists/Increment）。
- 分布式锁原语（`SetNx` / `Lock`）。

## 2. 目录结构

```
AspNetCore.Redis/
├── IRedisClient.cs                    # 客户端抽象
├── RedisClient.cs                     # 客户端实现（全局命名空间）
├── IRedisSerializer.cs                # 序列化抽象
├── JsonRedisSerializer.cs            # JSON 序列化实现
├── RedisKey.cs                        # 键生成器
├── RedisOptions.cs                    # 配置选项
└── RedisServiceCollectionExtensions.cs # DI 注册入口 AddRedis
```

## 3. 配置选项 `RedisOptions`

`RedisOptions.cs`：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ConnectionString` | `string` | `null!` | Redis 连接串（必填） |
| `Database` | `int` | `0` | 数据库索引 |
| `KeyPrefix` | `string` | `app:` | 键前缀 |

## 4. DI 注册 `AddRedis`

`RedisServiceCollectionExtensions.cs:16`（全局命名空间静态类）：

| 注册 | 生命周期 | 实现 |
| --- | --- | --- |
| `RedisOptions` | Singleton | `options`（回调配置后注册，工厂闭包共享同一实例） |
| `IConnectionMultiplexer` | Singleton | 工厂 `ConnectionMultiplexer.Connect(options.ConnectionString)` |
| `IRedisSerializer` | Singleton | `JsonRedisSerializer` |
| `IRedisClient` | Singleton | `RedisClient` |

> `RedisOptions` 注册为 Singleton，`RedisClient` 经容器激活时可解析其 ctor 参数。

## 5. 客户端 `RedisClient`

`RedisClient.cs:7`（全局命名空间），实现 `IRedisClient`。

依赖（构造注入）：

- `IConnectionMultiplexer connection` → `_db = connection.GetDatabase(options.Database)`（`RedisClient.cs:33`）。
- `IRedisSerializer _serializer`。
- `RedisOptions _options`。

### 5.1 键构建

`BuildKey(key) => $"{_options.KeyPrefix}{key}"`（`RedisClient.cs:43`）。所有操作自动加前缀。

### 5.2 操作方法

| 方法 | 说明 |
| --- | --- |
| `SetAsync<T>(key, value, expiry?)` | 序列化后 `StringSetAsync`；`expiry` 为 null 时用 `Expiration.Default`（`StackExchange.Redis.Expiration`，2.12+ 引入，表示无过期） |
| `GetAsync<T>(key)` | `StringGetAsync`，空值返回 `default`，否则反序列化 |
| `RemoveAsync(key)` | `KeyDeleteAsync` |
| `ExistsAsync(key)` | `KeyExistsAsync` |
| `IncrementAsync(key, value=1)` | `StringIncrementAsync` |
| `SetNxAsync(key, value, expiry)` | `StringSetAsync(..., When.NotExists)`，通用 NXSet 语义（键已存在返回 false） |
| `ExpireAsync(key, expiry)` | `KeyExpireAsync` |
| `LockAsync(key, value, expiry)` | `LockTakeAsync`，分布式锁获取（value 为持有者标识，如 Guid） |
| `LockReleaseAsync(key, value)` | `LockReleaseAsync`，CAS 释放锁（仅 value 匹配持有者时删除，防误释放他人锁） |

> `SetNxAsync`（通用 NXSet）与 `LockAsync`（锁获取，配 `LockReleaseAsync` 释放）语义已区分：`LockAsync` 用 SE.Redis 配套的 `LockTakeAsync`/`LockReleaseAsync`，释放走 CAS，仅持有者能解锁。

## 6. 序列化 `JsonRedisSerializer`

`JsonRedisSerializer.cs:9`，实现 `IRedisSerializer`。

- `Serialize<T>(value)` → `JsonSerializer.Serialize(value, Options)`。
- `Deserialize<T>(value)` → `JsonSerializer.Deserialize<T>(value, Options)`。
- `Options`：`PropertyNamingPolicy = CamelCase`（`JsonRedisSerializer.cs:14`）。

### `IRedisSerializer`

`IRedisSerializer.cs`：`string Serialize<T>(T)` + `T? Deserialize<T>(string)`。

## 7. 键生成器 `RedisKey`

`RedisKey.cs:6`，静态方法：

- `Build(prefix, key) => $"{prefix}{key}"`（`RedisKey.cs:14`）。
- `User(userId) => $"user:{userId}"`（`RedisKey.cs:22`）。
- `Order(orderId) => $"order:{orderId}"`（`RedisKey.cs:30`）。

> 注意：`RedisKey.Build` 与 `RedisClient.BuildKey` 各自独立实现前缀拼接；`User`/`Order` 方法返回的键**不含 `KeyPrefix`**，需调用方自行组合。TODO：两套前缀逻辑是否应统一待确认。

## 8. 接口 `IRedisClient`

`IRedisClient.cs:8`，方法签名与 `RedisClient` 实现一致（见 §5.2，含 `LockReleaseAsync`）。

## 9. 待确认事项（TODO）

- **TODO**：`RedisKey` 与 `RedisClient.BuildKey` 两套前缀逻辑，`RedisKey.User`/`Order` 返回的键不含 `KeyPrefix`，建议统一。
- **TODO**：当前解决方案中无任何项目引用 `AspNetCore.Redis`（API 的 csproj 仅引用 DataAccess）。该库尚无宿主集成路径，是否接入 API（缓存/限流/分布式锁）待确认。
