using System.Runtime.CompilerServices;

// 暴露 internal 类型给测试项目，便于单元测试覆盖纯逻辑
// （如 ActivityTracingInitializer 的 Activity listener 启动逻辑）。
[assembly: InternalsVisibleTo("AspNetCore.Logging.Tests")]
