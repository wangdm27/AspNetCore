using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AspNetCore.Api.Infrastructure.Middleware
{
    /// <summary>
    /// API 异常处理中间件
    /// 捕获请求处理过程中的异常，记录日志并返回统一格式的 JSON 错误响应
    /// </summary>
    public sealed class ApiExceptionMiddleware
    {
        /// <summary>
        /// 下一个中间件委托
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        /// <summary>
        /// 初始化异常处理中间件
        /// </summary>
        /// <param name="next">下一个中间件委托</param>
        /// <param name="logger">日志记录器</param>
        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// 处理请求并捕获异常
        /// </summary>
        /// <param name="context">HTTP 上下文</param>
        /// <returns>任务</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InvalidOperationException ex)
            {
                // 业务逻辑异常：记录警告日志，返回 400 Bad Request
                _logger.LogWarning(ex,
                    "业务异常：{Method} {Path} 返回 {StatusCode}。{Message}",
                    context.Request.Method,
                    context.Request.Path,
                    (int)HttpStatusCode.BadRequest,
                    ex.Message);
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                // 未处理的异常：记录错误日志（含完整堆栈），返回 500 Internal Server Error
                _logger.LogError(ex,
                    "未处理异常：{Method} {Path} 返回 {StatusCode}。TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    (int)HttpStatusCode.InternalServerError,
                    context.TraceIdentifier);
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred.");
            }
        }

        /// <summary>
        /// 写入错误响应
        /// </summary>
        /// <param name="context">HTTP 上下文</param>
        /// <param name="statusCode">HTTP 状态码</param>
        /// <param name="message">错误消息</param>
        /// <returns>任务</returns>
        private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new
            {
                code = (int)statusCode,
                message
            });

            await context.Response.WriteAsync(payload);
        }
    }
}
