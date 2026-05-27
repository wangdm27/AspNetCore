using System.Net;
using System.Text.Json;

namespace AspNetCore.Api.Infrastructure.Middleware
{
    public sealed class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InvalidOperationException ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception)
            {
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred.");
            }
        }

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
