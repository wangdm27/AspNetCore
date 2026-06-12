using System.Net;
using System.Text.Json;

namespace AspNetCore.Api.Infrastructure.Middleware
{
    /// <summary>
    /// API 异常处理中间件
    /// 捕获请求处理过程中的异常，并返回统一格式的 JSON 错误响应
    /// </summary>
    public sealed class ApiExceptionMiddleware
    {
        /// <summary>
        /// 下一个中间件委托
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// 初始化异常处理中间件
        /// </summary>
        /// <param name="next">下一个中间件委托</param>
        public ApiExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
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
                // 业务逻辑异常，返回 400 Bad Request
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception)
            {
                // 未处理的异常，返回 500 Internal Server Error
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