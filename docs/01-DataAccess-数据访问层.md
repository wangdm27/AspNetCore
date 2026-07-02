# 01 · DataAccess 数据访问层

> 项目：`AspNetCore.DataAccess/AspNetCore.DataAccess.csproj`
> 命名空间根：`AspNetCore.DataAccess`

## 1. 模块职责

提供**统一的数据访问抽象**，按配置在 **EF Core** 与 **Dapper** 两种 ORM 间切换，屏蔽上层对具体 ORM 的依赖。

对外能力：

- 单一入口 `AddUnifiedDataAccess<TDbContext>`（`ServiceCollectionExtensions.cs:23`）完成全部注册。
- 统一抽象：`IRepository<TEntity>`、`IUnitOfWork`、`IDbConnectionFactory`、`IConnectionStringResolver`。
- 支持 `SqlServer` 与 `PostgreSql` 两种提供程序。
- EF Core 模型与实体配置集中在 `ApplicationDbContext`。

## 2. 目录结构

```
AspNetCore.DataAccess/
├── Abstractions/              # 对外抽象接口（不依赖具体 ORM）
│   ├── IRepository.cs
│   ├── IUnitOfWork.cs
│   ├── IDbConnectionFactory.cs
│   └── IConnectionStringResolver.cs
├── Dapper/                    # Dapper 实现
│   ├── IDapperContext.cs
│   ├── DapperContext.cs
│   ├── DapperRepository.cs
│   └── DapperUnitOfWork.cs
├── EntityFramework/          # EF Core 实现
│   ├── IRepositoryDbContext.cs
│   ├── EfRepositoryDbContext.cs
│   ├── EfRepository.cs
│   └── EfUnitOfWork.cs
├── Internal/                  # 内部实现（连接解析、连接工厂）
│   ├── ConnectionStringResolver.cs
│   └── DbConnectionFactory.cs
├── Entities/                  # 实体（数据模型）
│   ├── Enums/PermissionType.cs
│   ├── User.cs / Tenant.cs / TenantUser.cs
│   ├── Role.cs / Permission.cs / RolePermission.cs / UserRole.cs
│   ├── Menu.cs / AuditLog.cs / RefreshToken.cs / SampleEntity.cs
├── ApplicationDbContext.cs    # EF Core DbContext + 实体配置
├── DatabaseOptions.cs         # Database 配置选项
├── DatabaseProvider.cs        # 提供程序枚举
├── OrmType.cs                 # ORM 枚举
└── ServiceCollectionExtensions.cs   # DI 注册入口
```

## 3. 配置与注册

### 3.1 配置选项 `DatabaseOptions`

`DatabaseOptions.cs`，节名 `Database`：

| 属性 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Provider` | `DatabaseProvider` | `SqlServer` | 数据库提供程序 |
| `Orm` | `OrmType` | `EntityFrameworkCore` | ORM 类型 |
| `ConnectionStringName` | `string?` | `null` | 连接串名；为空时用 `Provider.ToString()` |
| `CommandTimeoutSeconds` | `int` | `30` | 命令超时秒数 |

### 3.2 枚举

- `DatabaseProvider`（`DatabaseProvider.cs`）：`SqlServer` / `PostgreSql`。
- `OrmType`（`OrmType.cs:6`）：`EntityFrameworkCore = 1` / `Dapper = 2`。

### 3.3 注册流程 `AddUnifiedDataAccess<TDbContext>`

`ServiceCollectionExtensions.cs:23`：

1. 绑定 `DatabaseOptions`（`ServiceCollectionExtensions.cs:28`）。
2. 注册 `IConnectionStringResolver`（Singleton）、`IDbConnectionFactory`（Scoped）。
3. 解析连接串，调用 `AddDbContext<TDbContext>`，按 `Provider` 配置 `UseSqlServer` / `UseNpgsql`，并启用 `EnableRetryOnFailure`（重试 3 次、间隔 10s）与命令超时（`ServiceCollectionExtensions.cs:86`）。
4. 按 `Orm` 分支：
   - `EntityFrameworkCore` → `RegisterEntityFramework`（`ServiceCollectionExtensions.cs:60`）：注册 `IRepositoryDbContext`、`IRepository<>`、`IUnitOfWork`。
   - `Dapper` → `RegisterDapper`（`ServiceCollectionExtensions.cs:72`）：注册 `IDapperContext`、`IRepository<>`、`IUnitOfWork`。

> 注意：`IDbConnectionFactory` 始终注册（即使选 EF Core），Dapper 实现也依赖它。

## 4. 抽象层 `Abstractions/`

### `IRepository<TEntity>`（`IRepository.cs:9`）

泛型仓储，约束 `TEntity : class`：

| 方法 | 说明 |
| --- | --- |
| `GetByIdAsync(object id, ct)` | 按主键查单条 |
| `GetAllAsync(ct)` | 全量查询 |
| `FindAsync(Expression predicate, ct)` | 按条件查询 |
| `AddAsync(entity, ct)` | 新增 |
| `UpdateAsync(entity, ct)` | 更新 |
| `DeleteAsync(object id, ct)` | 按主键删除 |

### `IUnitOfWork`（`IUnitOfWork.cs:6`）

仅 `SaveChangesAsync(ct) -> Task<int>`。事务由实现各自管理。

### `IConnectionStringResolver`（`Abstractions/IConnectionStringResolver.cs`）

`ResolveConnectionString(DatabaseOptions, IConfiguration) -> string`。

### `IDbConnectionFactory`（`Abstractions/IDbConnectionFactory.cs`）

`CreateConnection() -> IDbConnection`。

## 5. 连接解析 `Internal/`

### `ConnectionStringResolver`（`ConnectionStringResolver.cs:9`）

- 连接串名优先取 `options.ConnectionStringName`，为空回退 `options.Provider.ToString()`（`ConnectionStringResolver.cs:20`）。
- 从 `IConfiguration.GetConnectionString(name)` 取值；为空抛 `InvalidOperationException`。

### `DbConnectionFactory`（`DbConnectionFactory.cs:13`）

- 注入 `IOptions<DatabaseOptions>`、`IConfiguration`、`IConnectionStringResolver`。
- `CreateConnection()` 按 `Provider` 返回 `SqlConnection` 或 `NpgsqlConnection`；未知值抛 `NotSupportedException`。

## 6. EF Core 实现 `EntityFramework/`

### `EfRepositoryDbContext<TDbContext>`（`EfRepositoryDbContext.cs:9`）

实现 `IRepositoryDbContext`，封装 `TDbContext`。提供 `Set<T>()`、`FindAsync`、`AddAsync`、`Update`、`Remove`、`SaveChangesAsync`。

### `EfRepository<TEntity>`（`EfRepository.cs:11`）

- 依赖 `IRepositoryDbContext`。
- `GetAllAsync` / `FindAsync` 使用 `AsNoTracking()`（不跟踪）。
- `DeleteAsync` 先 `FindAsync` 再 `Remove`（不存在则跳过）。
- `AddAsync` / `UpdateAsync` 仅变更跟踪，不立即保存——由 `IUnitOfWork` 统一提交。

### `EfUnitOfWork`（`EfUnitOfWork.cs:8`）

`SaveChangesAsync` 直接委托 `IRepositoryDbContext.SaveChangesAsync`（EF 内置变更跟踪 + 事务）。

## 7. Dapper 实现 `Dapper/`

### `IDapperContext` / `DapperContext`（`DapperContext.cs:9`）

- 构造时即通过 `IDbConnectionFactory.CreateConnection()` 建立连接（`DapperContext.cs:21`）。
- 暴露 `Connection`（`IDbConnection`）与 `Transaction`（`IDbTransaction?`）。
- `BeginTransactionAsync`：连接未打开时先 `OpenAsync`，再 `Connection.BeginTransaction()`（`DapperContext.cs:38`）。
- `CommitAsync` / `RollbackAsync`：提交/回滚并 `Dispose` 事务。
- 实现 `IAsyncDisposable`，`Dispose` 释放事务与连接。
- Scoped 生命周期，每个请求一个上下文。

### `DapperRepository<TEntity>`（`DapperRepository.cs:15`）

通过反射 + Data Annotation 特性构建实体元数据，生成 SQL：

- **写操作（Add/Update/Delete）**先 `EnsureTransactionAsync` 确保存在事务（`DapperRepository.cs:118`），再执行。
- `FindAsync` **在内存中过滤**（`FindInMemoryAsync`，`DapperRepository.cs:132`）：先 `GetAllAsync` 全表拉取，再用 `predicate.Compile()` 过滤。

> 注意：`FindAsync` 的内存过滤对大表有性能/内存风险。文档记录现状，不评判。

#### 实体元数据 `EntityMetadata`（`DapperRepository.cs:160`）

`Create()` 反射构建：

- 表名：`[Table]` 特性名，否则类名（`DapperRepository.cs:199`）。
- 主键：`[Key]` 特性 → 名为 `Id` → 名为 `{EntityName}Id`，否则抛 `InvalidOperationException`（`DapperRepository.cs:204`）。
- 列名：`[Column]` 特性名，否则属性名。
- 自增列：`[DatabaseGenerated(Identity)]` 的主键排除出 INSERT 列表（`DapperRepository.cs:209`）。
- `[NotMapped]` 属性跳过。

### `DapperUnitOfWork`（`DapperUnitOfWork.cs:8`）

`SaveChangesAsync` 即 `_dapperContext.CommitAsync()`，固定返回 `1`（非真实受影响行数，`DapperUnitOfWork.cs:29`）。

> 注意：Dapper 路径下，**只有写操作才会触发 `BeginTransactionAsync`**；纯读操作不开启事务。`SaveChangesAsync` 提交事务。若请求中只有读操作却调用 `SaveChangesAsync`，`Transaction` 为 null，`CommitAsync` 中 `Transaction?.Commit()` 为空操作——不报错但也不做任何事。

## 8. 数据模型 `ApplicationDbContext` + `Entities/`

### 8.1 DbContext

`ApplicationDbContext.cs:6`，11 个 `DbSet`：

| DbSet | 实体 | 表名 |
| --- | --- | --- |
| `Menus` | `Menu` | `menus` |
| `AuditLogs` | `AuditLog` | `audit_logs` |
| `Permissions` | `Permission` | `permissions` |
| `RefreshTokens` | `RefreshToken` | `refresh_tokens` |
| `Roles` | `Role` | `roles` |
| `RolePermissions` | `RolePermission` | `role_permissions` |
| `SampleEntities` | `SampleEntity` | `sample_entities` |
| `Tenants` | `Tenant` | `tenants` |
| `TenantUsers` | `TenantUser` | `tenant_users` |
| `Users` | `User` | `users` |
| `UserRoles` | `UserRole` | `user_roles` |

实体配置集中在 `OnModelCreating`（`ApplicationDbContext.cs:25`），各 `Configure*` 私有方法定义索引、列约束、外键级联。关键约束：

- `User`：`UserName`、`Email` 唯一索引；`PasswordHash` 512 / `PasswordSalt` 256 长度。
- `Tenant`：`Code` 唯一。
- `Role`：复合唯一索引 `(TenantId, Code)`、`(TenantId, Name)`。
- `Permission`：`Code` 唯一。
- `TenantUser`：复合主键 `(TenantId, UserId)`，双外键级联删除。
- `RolePermission`：复合主键 `(RoleId, PermissionId)`。
- `UserRole`：复合主键 `(TenantId, UserId, RoleId)`。
- `Menu`：自引用 `Parent`/`Children`，`DeleteBehavior.Restrict`。
- `RefreshToken`：`TokenHash` 唯一索引。

### 8.2 实体一览

- **User**（`Entities/User.cs`）：`Id`、`UserName`、`Email`、`DisplayName`、`PasswordHash`、`PasswordSalt`、`IsActive`、`CreatedAt`、`UpdatedAt`。导航：`TenantUsers`、`UserRoles`。
- **Tenant**（`Tenant.cs`）：`Id`、`Code`、`Name`、`IsActive`、`CreatedAt`、`UpdatedAt`。导航：`Roles`、`TenantUsers`。
- **TenantUser**（`TenantUser.cs`）：`TenantId`、`UserId`、`IsTenantOwner`、`JoinedAt`。多对多连接表。
- **Role**（`Role.cs`）：`TenantId`、`Code`、`Name`、`Description`、`IsDefault`、`CreatedAt`、`UpdatedAt`。导航：`Tenant`、`UserRoles`、`RolePermissions`。
- **Permission**（`Permission.cs`）：`Code`、`Name`、`Type`(`PermissionType`)、`Description`、`HttpMethod`、`Route`、`CreatedAt`。
- **RolePermission**（`RolePermission.cs`）：`RoleId`、`PermissionId`、`GrantedAt`。
- **UserRole**（`UserRole.cs`）：`TenantId`、`UserId`、`RoleId`、`AssignedAt`。
- **Menu**（`Menu.cs`）：`Id`、`ParentId?`、`Code`、`Name`、`Path`、`Component`、`Icon`、`Sort`、`PermissionCode`、`CreatedAt`。自引用树。
- **AuditLog**（`AuditLog.cs`）：`TenantId?`、`UserId?`、`UserName`、`Action`、`EntityType`、`EntityId?`、`Details`、`IpAddress`、`UserAgent`、`CreatedAt`。
- **RefreshToken**（`RefreshToken.cs`）：`UserId`、`TokenHash`、`ExpiresAt`、`IsUsed`、`CreatedAt`。
- **SampleEntity**（`SampleEntity.cs`）：`Id`、`Name`、`CreatedAt`。示例实体。
- **PermissionType**（`Enums/PermissionType.cs`）：`Api=1` / `Menu=2` / `Button=3`。

## 9. 依赖关系图

```
上层 (Api)
   │ 依赖
   ▼
IRepository<T> ──┬── EfRepository<T>     ── EfRepositoryDbContext<TDbContext> ── TDbContext (ApplicationDbContext)
   (抽象)       │
                └── DapperRepository<T> ── DapperContext ── IDbConnectionFactory ── DbConnectionFactory
                                                                              └── IConnectionStringResolver ── ConnectionStringResolver

IUnitOfWork ──┬── EfUnitOfWork     ── IRepositoryDbContext
              └── DapperUnitOfWork ── DapperContext (CommitAsync)
```

## 10. 待确认事项（TODO）

- **TODO**：`ApplicationDbContext` 使用 `EnsureCreatedAsync`（见 `DatabaseInitializationHostedService.cs:20`）创建库结构，**未启用 EF 迁移（Migrations）**。schema 变更如何管理需确认。
- **TODO**：Dapper 与 EF 双实现并存，但 `ApplicationDbContext.OnModelCreating` 的列名/表名约定仅对 EF 生效；Dapper 路径依赖 `[Table]`/`[Column]`/`[Key]` 特性（实体已标注，二者一致）。若未来在 EF 端用 `HasColumnName` 改列名而实体特性未同步，Dapper 路径将不一致——需约定单一事实来源。
- **TODO**：Dapper `FindAsync` 全表内存过滤（`DapperRepository.cs:132`），是否为有意简化待确认；生产场景可能需替换为参数化 SQL。
- **TODO**：`IDbConnectionFactory` 在 EF Core 路径下注册但未被 EF 仓储使用；是否预留给手写 SQL 场景待确认。
