# 04 · Api 应用服务层

> 项目：`AspNetCore.Api/AspNetCore.Api.csproj`
> 命名空间根：`AspNetCore.Api`
> 依赖：`AspNetCore.DataAccess`、`Microsoft.AspNetCore.Authentication.JwtBearer`、`Microsoft.AspNetCore.OpenApi`

## 1. 模块职责

多租户 + RBAC 权限的 REST API。核心能力：

- **认证**：JWT 访问令牌 + Refresh Token（哈希存储）；PBKDF2 密码哈希。
- **授权**：基于权限码（`PermissionAuthorize`）的请求级权限校验。
- **多租户**：租户/用户/角色/权限隔离，租户创建即生成管理员与成员角色。
- **业务模块**：Identity（认证用户）、Authorization（角色/权限/菜单）、Tenancy（租户）。
- **横切**：统一异常处理、当前请求上下文、审计日志、SMTP 邮件、数据库初始化种子。

## 2. 目录结构

```
AspNetCore.Api/
├── Program.cs                       # 入口与中间件管线
├── appsettings.json / .Development.json
├── Controllers/                     # HTTP 端点
├── Infrastructure/
│   ├── Auth/                        # 密码哈希、JWT、选项
│   ├── Context/                     # 当前请求上下文（用户/租户）
│   ├── Services/                    # 审计、邮件
│   ├── Middleware/                  # 异常处理中间件
│   ├── Extensions/                  # ServiceCollectionExtensions、HttpContextExtensions
│   └── DatabaseInitializationHostedService.cs
└── Modules/
    ├── Identity/   (Services + Contracts + Models)   # 认证、用户
    ├── Authorization/ (Services + Contracts + 过滤器/特性) # 角色、权限、菜单
    └── Tenancy/    (Services + Contracts)            # 租户
```

## 3. 启动与管线

入口 `Program.cs`（详见 [00-项目总览.md §5](./00-项目总览.md)）。管线顺序：

```
(Dev) MapOpenApi
UseMiddleware<ApiExceptionMiddleware>      # 统一异常
UseHttpsRedirection
UseCors("AllowAll")
UseAuthentication → UseAuthorization
MapControllers
```

服务注册：

- 数据访问：`AddUnifiedDataAccess<ApplicationDbContext>`。
- 业务模块 + JWT + 邮件 + 审计 + `DatabaseInitializationHostedService`：`AddBusinessModules`（`Infrastructure/Extensions/ServiceCollectionExtensions.cs:15`）。

## 4. 横切基础设施 `Infrastructure/`

### 4.1 异常中间件 `ApiExceptionMiddleware`

`Infrastructure/Middleware/ApiExceptionMiddleware.cs:10`：

- `InvalidOperationException` → `400 Bad Request`（业务异常）。
- 其他异常 → `500`，统一消息 `"An unexpected server error occurred."`（不泄露细节）。
- 响应体：`{ code, message }` JSON。

### 4.2 当前请求上下文 `CurrentRequestContext`

`Infrastructure/Context/CurrentRequestContext.cs:10`，实现 `ICurrentRequestContext`，Scoped。

| 属性 | 来源 |
| --- | --- |
| `UserId` (`Guid?`) | Claim `NameIdentifier` |
| `TenantId` (`Guid?`) | Claim `tenant_id`，其次请求头 `X-Tenant-Id` |
| `UserName` (`string?`) | Claim `Name` |
| `TenantCode` (`string?`) | Claim `tenant_code`，其次请求头 `X-Tenant-Code` |
| `IsAuthenticated` (`bool`) | `HttpContext.User.Identity.IsAuthenticated` |

> 租户可由 Claim 或请求头提供；Claim 优先。

### 4.3 HttpContext 扩展

`Infrastructure/Extensions/HttpContextExtensions.cs`：

- `GetRequiredUserId()`：从 `NameIdentifier` Claim 解析 `Guid`，失败抛异常。
- `GetRequiredTenantId()`：Claim `tenant_id` 优先，回退请求头 `X-Tenant-Id`。

### 4.4 数据库初始化 `DatabaseInitializationHostedService`

`Infrastructure/DatabaseInitializationHostedService.cs:7`（`IHostedService`）：

- `StartAsync`：建作用域 → `ApplicationDbContext.Database.EnsureCreatedAsync` → `IAuthorizationSeedService.SeedAsync`。
- 未使用 EF Migrations（见 [01 §10](./01-DataAccess-数据访问层.md)）。

### 4.5 认证基础设施 `Infrastructure/Auth/`

| 类型 | 文件 | 说明 |
| --- | --- | --- |
| `IPasswordHasher` / `Pbkdf2PasswordHasher` | `Pbkdf2PasswordHasher.cs:9` | PBKDF2-SHA256，迭代 100000，盐 16B，哈希 32B，Base64 存储；`Verify` 用 `FixedTimeEquals` 防时序攻击 |
| `ITokenService` / `JwtTokenService` | `JwtTokenService.cs:15` | 签发 Access Token；生成 64B 随机 Refresh Token；`GetPrincipalFromExpiredToken`（忽略过期验签，预留） |
| `JwtOptions` | `JwtOptions.cs` | `Issuer`/`Audience`/`SigningKey`/`AccessTokenExpiresMinutes`(120)/`RefreshTokenExpiresDays`(7) |

JWT Claims（`JwtTokenService.cs:36`）：`sub`、`unique_name`、`NameIdentifier`、`Name`、`display_name`、`tenant_id`、`tenant_code`，外加 `Role`（角色码）与 `permission`（权限码）多条 Claim。

### 4.6 审计 `AuditLogService`

`Infrastructure/Services/AuditLogService.cs:11`，实现 `IAuditLogService`，Scoped。

- `LogAsync`（`AuditLogService.cs:30`）：从 `ICurrentRequestContext` 取用户/租户，从 `HttpContext` 取 IP、UserAgent，构造 `AuditLog` 加入 `DbContext`。**不 SaveChanges**——由调用方事务统一提交。
- `QueryAsync`（`AuditLogService.cs:63`）：多条件 + 分页查询。

> **TODO**：`IAuditLogService.LogAsync`（写审计）**全代码库无调用方**（已 grep 确认，仅注册与查询端使用）。审计"写入"路径当前为死代码，未在任何业务操作中触发。是否计划接入待确认。

### 4.7 邮件 `SmtpEmailService`

`Infrastructure/Services/SmtpEmailService.cs:10`，`IEmailService` 实现。

- `SendPasswordResetEmailAsync`：用 `EmailOptions.PasswordResetUrl` 替换 `{token}`，构造 HTML 邮件（说明 15 分钟有效）。
- `SendAsync`：`MailMessage` + `SmtpClient`（`EnableSsl`、`NetworkCredential`）。

`EmailOptions`（`EmailOptions.cs`）：SMTP 主机/端口/凭据/发件人 + `PasswordResetUrl` 模板。

## 5. 业务模块

### 5.1 Identity 模块（`Modules/Identity/`）

#### `AuthService`（`Services/AuthService.cs:19`）

| 方法 | 流程 |
| --- | --- |
| `RegisterAsync` | 校验租户存在且激活 → 用户名/邮箱唯一 → PBKDF2 哈希 → 建 User + TenantUser + 默认角色(`IsDefault`) → 签发令牌 |
| `LoginAsync` | 按 `TenantCode` + `UserName` 查 → 校验租户/用户激活 + `TenantUser` 存在 → `Verify` 密码 → 签发令牌 |
| `GetCurrentUserProfileAsync` | 返回用户资料 + 角色 + 权限码 |
| `RefreshTokenAsync` | 按 token **SHA256 哈希**查 `RefreshToken` → 校验未用/未过期 → 标记已用 → 选最近活跃租户 → 签发新令牌 + 存新 RefreshToken |
| `ChangePasswordAsync` | 验旧密码 → 更新哈希/盐 → **撤销所有未用 RefreshToken** |
| `ForgotPasswordAsync` | 生成 15 分钟 JWT 重置 token（claim `purpose=password_reset`）→ 发邮件；租户/用户不存在时**静默返回**（防信息泄露） |
| `ResetPasswordAsync` | 验证重置 token → 更新密码 → 撤销所有未用 RefreshToken |

> RefreshToken 以 SHA256 哈希存储（`AuthService.cs:484`），数据库不存明文。改密/重置密码会吊销未用 RefreshToken。

#### `UserService`（`Services/UserService.cs:13`）

| 方法 | 说明 |
| --- | --- |
| `GetTenantUsersAsync` | 分页 + 关键词(用户名/显示名/邮箱) + 状态过滤，含每用户角色码 |
| `GetAsync` | 用户详情 + 角色 + 权限码 |
| `CreateAsync` | 校验租户激活 + 用户名/邮箱唯一 + 角色属本租户 → 建用户 + TenantUser + UserRoles |
| `UpdateAsync` | 更新显示名/邮箱/激活态；校验邮箱被他人占用 |
| `DeleteAsync` | 禁止删自己、禁止删租户所有者；移除本租户角色与 TenantUser；若用户无其他租户则标记 `IsActive=false` |
| `AssignRolesAsync` | **替换**现有角色（先删后增） |
| `ResetPasswordAsync` | 管理员重置密码（无需旧密码） |

### 5.2 Authorization 模块（`Modules/Authorization/`）

#### 权限校验机制

1. `PermissionAuthorizeAttribute(permissionCode)`（`PermissionAuthorizeAttribute.cs:6`）：`TypeFilterAttribute`，绑定 `PermissionAuthorizationFilter`，把权限码作为参数。
2. `PermissionAuthorizationFilter`（`PermissionAuthorizationFilter.cs:8`，`IAsyncAuthorizationFilter`）：
   - 未认证 / 无 `UserId` / 无 `TenantId` → `UnauthorizedResult`。
   - `IPermissionChecker.HasPermissionAsync(tenantId, userId, permissionCode)` 为 false → `ForbidResult`。
3. `PermissionChecker`（`Services/PermissionChecker.cs:6`）：联查 `UserRoles → RolePermissions → Permissions`，按 `(tenantId, userId, permissionCode)` 判存在性。

> 控制器同时标注 `[Authorize]`（JWT 认证）与 `[PermissionAuthorize("xxx")]`（权限码）。

#### 服务

| 接口 | 说明 |
| --- | --- |
| `IRoleService` | 角色 CRUD、`GetRolePermissionsAsync`、`AssignPermissionsAsync`、`AssignMenusAsync` |
| `IPermissionService` | `GetPermissionsAsync`、`GetCurrentMenusAsync`（用户菜单）、`GetCurrentRoutesAsync`（用户路由） |
| `IPermissionChecker` | 请求级权限校验 |
| `IAuthorizationSeedService` | 启动时种子权限与菜单 |

#### `AuthorizationSeedService`（`Services/AuthorizationSeedService.cs:8`）

`SeedAsync`（`AuthorizationSeedService.cs:42`）：

- 以**固定 Guid** 种子化 18 个权限（`tenant.*`/`user.*`/`role.*`/`permission.view`/`menu.view`/`audit.view`），含 `Code`/`Name`/`Type`/`HttpMethod`/`Route`。
- 已存在的权限按种子更新元数据（幂等），新权限插入。
- 种子化 5 个根菜单（Tenant/User/Role/Permission/Audit Center），各绑定 `PermissionCode`。
- 仅当有变更时 `SaveChangesAsync`。

### 5.3 Tenancy 模块（`Modules/Tenancy/`）

#### `TenantService`（`Services/TenantService.cs:13`）

| 方法 | 说明 |
| --- | --- |
| `GetAllAsync` | 租户列表，按创建时间升序 |
| `CreateAsync` | **创建租户即初始化**：建 Tenant + 管理员 User + `tenant_admin` 角色（授予**全部权限**）+ `tenant_member` 默认角色（仅 `menu.view`/`user.view`）+ TenantUser(owner) + UserRole(admin) |
| `GetByIdAsync` | 租户详情 |
| `AddUserAsync` | 将已存在用户绑定到租户，分配默认角色 |
| `UpdateAsync` | 更新租户名/激活态 |

> 租户管理员角色 `tenant_admin` 在创建时获得**所有权限 ID**（`TenantService.cs:144`），即新租户管理员拥有系统全部权限码。

## 6. 控制器与端点

所有控制器均 `[ApiController]`。下表列出端点（来源：`Controllers/*.cs`）。

### AuthController `api/identity/auth`（`AuthController.cs:11`）

| 方法 | 路由 | 鉴权 | 权限码 |
| --- | --- | --- | --- |
| POST | `register` | AllowAnonymous | — |
| POST | `login` | AllowAnonymous | — |
| GET | `me` | Authorize | — |
| POST | `refresh` | AllowAnonymous | — |
| PUT | `change-password` | Authorize | — |
| POST | `forgot-password` | AllowAnonymous | — |
| POST | `reset-password` | AllowAnonymous | — |

### UsersController `api/identity/users`（`UsersController.cs:12`，类级 `[Authorize]`）

| 方法 | 路由 | 权限码 |
| --- | --- | --- |
| GET | `` | `user.view` |
| GET | `{userId:guid}` | `user.view` |
| POST | `` | `user.create` |
| PUT | `{userId:guid}` | `user.update` |
| DELETE | `{userId:guid}` | `user.delete` |
| PUT | `{userId:guid}/roles` | `user.assign_roles` |
| PUT | `{userId:guid}/password` | `user.update` |

### RolesController `api/authorization/roles`（`RolesController.cs:12`，类级 `[Authorize]`）

| 方法 | 路由 | 权限码 |
| --- | --- | --- |
| GET | `` | `role.view` |
| GET | `{roleId:guid}` | `role.view` |
| POST | `` | `role.create` |
| PUT | `{roleId:guid}` | `role.update` |
| DELETE | `{roleId:guid}` | `role.delete` |
| GET | `{roleId:guid}/permissions` | `role.view` |
| PUT | `{roleId:guid}/permissions` | `role.assign_permissions` |
| PUT | `{roleId:guid}/menus` | `role.assign_menus` |

### PermissionsController `api/authorization/permissions`（`PermissionsController.cs:12`）

| 方法 | 路由 | 权限码 |
| --- | --- | --- |
| GET | `` | `permission.view` |

### MenusController `api/authorization/menus`（`MenusController.cs:13`）

| 方法 | 路由 | 权限码 |
| --- | --- | --- |
| GET | `current` | `menu.view` |
| GET | `current/routes` | `menu.view` |

### TenantsController `api/tenancy/tenants`（`TenantsController.cs:11`）

| 方法 | 路由 | 鉴权 | 权限码 |
| --- | --- | --- | --- |
| GET | `` | Authorize | `tenant.view` |
| POST | `` | **AllowAnonymous** | — |
| GET | `current` | Authorize | `tenant.view` |
| PUT | `{tenantId:guid}` | Authorize | `tenant.update` |
| POST | `current/users` | Authorize | `tenant.user.add` |

> 租户创建 `POST /api/tenancy/tenants` 为 `AllowAnonymous`——新租户注册无需认证（创建后产出管理员凭据）。

### AuditLogsController `api/audit-logs`（`AuditLogsController.cs:11`，类级 `[Authorize]`）

| 方法 | 路由 | 权限码 |
| --- | --- | --- |
| GET | `` | `audit.view` |

### SampleEntitiesController `api/[controller]`（`SampleEntitiesController.cs:8`）

| 方法 | 路由 | 鉴权 |
| --- | --- | --- |
| GET | `` | 无 |
| GET | `{id:guid}` | 无 |
| POST | `` | 无 |

> 无鉴权的示例端点，仅演示数据访问。`WeatherForecastController` 为模板默认，略。

## 7. 请求流

```
HTTP 请求
  └─ ApiExceptionMiddleware（捕获异常 → 400/500）
       └─ Authentication（JWT → Claims: user/tenant/role/permission）
            └─ Authorization（[Authorize]）
                 └─ PermissionAuthorizationFilter（[PermissionAuthorize("code")]）
                      ├─ ICurrentRequestContext 取 tenantId/userId
                      └─ IPermissionChecker.HasPermissionAsync(tenantId, userId, code)
                           └─ Controller → Service(ApplicationDbContext) → SaveChangesAsync
```

业务服务直接注入 `ApplicationDbContext`（EF Core 路径），未走 `IRepository<T>`/`IUnitOfWork` 抽象。TODO：见 §9。

## 8. 数据库初始化与种子

启动顺序（`DatabaseInitializationHostedService`）：

1. `EnsureCreatedAsync` 建库（无迁移）。
2. `AuthorizationSeedService.SeedAsync`：权限（18 项）+ 菜单（5 根）。
3. （租户/管理员在 `POST /api/tenancy/tenants` 时按需创建。）

> 注意：种子仅含权限与菜单，**不预置租户/用户**。首个租户需通过 `POST /api/tenancy/tenants`（匿名）创建。

## 9. 待确认事项（TODO）

- **TODO**：`IAuditLogService.LogAsync` 写入路径无调用方（见 §4.6），审计写入未接入业务流程。
- **TODO**：业务服务直接使用 `ApplicationDbContext`，**未使用** `DataAccess` 提供的 `IRepository<T>`/`IUnitOfWork` 抽象。DataAccess 的 Dapper 路径在当前 API 中实际未启用（`appsettings.json` 默认 `Orm=EntityFrameworkCore`）。Dapper 路径是否有计划使用待确认。
- **TODO**：`JwtTokenService.GetPrincipalFromExpiredToken` 定义但无调用方（Refresh 流程用独立 RefreshToken 表，非过期 Access Token 提取）。是否预留待确认。
- **TODO**：`SampleEntitiesController` 与 `WeatherForecastController` 无鉴权，是否应在生产移除待确认。
- **TODO**：租户管理员（`tenant_admin`）在租户创建时被授予**全部权限**（含其他租户相关权限码如 `tenant.create`）。权限码是否应按租户隔离范围裁剪待确认。
- **TODO**：`AuditLogsController.cs:11` 处 `[Route]` 特性位于类上方、`[ApiController]`/`[Authorize]` 之后、XML 注释之前——特性与文档注释交错，编译可过但建议整理。
