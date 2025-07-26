using System.Net.WebSockets;

namespace CollaborativePuzzle.Api.WebSockets
{
    /// <summary>
    /// Middleware for handling raw WebSocket connections
    /// </summary>
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        public WebSocketMiddleware(
            RequestDelegate next,
            ILogger<WebSocketMiddleware> logger,
            IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    
                    _logger.LogInformation("WebSocket connection accepted from {RemoteIp}", 
                        context.Connection.RemoteIpAddress);

                    // Create a scoped handler for this connection
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<WebSocketHandler>();
                    
                    await handler.HandleWebSocketAsync(webSocket);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("WebSocket connections only");
                }
            }
            else
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Extension methods for WebSocket middleware
    /// </summary>
    public static class WebSocketMiddlewareExtensions
    {
        public static IApplicationBuilder UseWebSocketMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<WebSocketMiddleware>();
        }
    }
}