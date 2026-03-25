using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace APITeamsV3.API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception has occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Ensure CORS headers are present even on failure
            // Note: If UseCors was already called and configured, 
            // the response might already have the headers. 
            // But if it crashed before reaching UseCors or in a way that cleared them, 
            // we might want to ensure them here if possible.
            // However, modifying headers after the response has started will fail.

            var result = JsonSerializer.Serialize(new
            {
                error = "Internal Server Error",
                message = exception.Message,
                detail = exception.InnerException?.Message
            });

            return context.Response.WriteAsync(result);
        }
    }
}
