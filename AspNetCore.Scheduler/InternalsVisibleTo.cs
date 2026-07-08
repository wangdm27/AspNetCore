using System.Runtime.CompilerServices;

// 暴露 internal 类型给测试项目，便于单元测试覆盖纯逻辑
// （如 HangfireDbInitializer 的库名提取与防注入校验）。
[assembly: InternalsVisibleTo("AspNetCore.Scheduler.Tests")]
