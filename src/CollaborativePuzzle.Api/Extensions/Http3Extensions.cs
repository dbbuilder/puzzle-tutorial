using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;

namespace CollaborativePuzzle.Api.Extensions;

public static class Http3Extensions
{
    public static WebApplicationBuilder ConfigureHttp3(this WebApplicationBuilder builder)
    {
        // Configure Kestrel for HTTP/3
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            // Enable HTTP/3
            options.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                listenOptions.UseHttps();
            });

            // Alternative: Configure specific ports for different protocols
            // HTTP/1.1 and HTTP/2
            options.ListenAnyIP(5000, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });

            // Configure limits for HTTP/3
            options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30MB
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB
            
            // Configure HTTP/3 specific settings
            options.ConfigureHttpsDefaults(httpsOptions =>
            {
                httpsOptions.OnAuthenticate = (context, sslOptions) =>
                {
                    // Configure SSL options for QUIC
                    sslOptions.ApplicationProtocols = new List<SslApplicationProtocol>
                    {
                        new SslApplicationProtocol("h3")
                    };
                };
            });
        });

        // Add HTTP/3 support to the service collection
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                // Enable HTTP/3 for all endpoints by default
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            });
        });

        // Add Alt-Svc header support for HTTP/3 discovery
        builder.Services.AddHttpContextAccessor();
        
        return builder;
    }

    public static IApplicationBuilder UseHttp3AltSvc(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            // Add Alt-Svc header to advertise HTTP/3 support
            if (context.Request.IsHttps)
            {
                var altSvcHeaderValue = $"h3=\":443\"; ma=86400";
                context.Response.Headers.Append("Alt-Svc", altSvcHeaderValue);
            }

            await next();
        });

        return app;
    }
}

public static class Http3Endpoints
{
    public static void MapHttp3Endpoints(this WebApplication app)
    {
        var http3Group = app.MapGroup("/api/http3")
            .WithTags("HTTP/3 Demo")
            .WithOpenApi();

        // GET /api/http3/info
        http3Group.MapGet("/info", (HttpContext context) =>
        {
            var protocol = context.Request.Protocol;
            var isHttp3 = protocol == "HTTP/3" || protocol.StartsWith("HTTP/3");
            
            return Results.Ok(new
            {
                protocol = protocol,
                isHttp3 = isHttp3,
                isHttps = context.Request.IsHttps,
                scheme = context.Request.Scheme,
                host = context.Request.Host.ToString(),
                connectionId = context.Connection.Id,
                localPort = context.Connection.LocalPort,
                remotePort = context.Connection.RemotePort,
                features = new
                {
                    hasHttp3Feature = context.Features.Any(f => f.Key.Name.Contains("Http3")),
                    hasQuicFeature = context.Features.Any(f => f.Key.Name.Contains("Quic"))
                },
                headers = context.Request.Headers
                    .Where(h => h.Key.StartsWith("Alt", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(h => h.Key, h => h.Value.ToString())
            });
        })
        .WithName("GetHttp3Info")
        .WithSummary("Get HTTP/3 connection information")
        .WithDescription("Returns detailed information about the current HTTP/3 connection");

        // GET /api/http3/performance-test
        http3Group.MapGet("/performance-test", async (HttpContext context, int size = 1024) =>
        {
            var startTime = DateTime.UtcNow;
            var protocol = context.Request.Protocol;
            
            // Generate test data
            var data = new byte[size * 1024]; // size in KB
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
            
            // Measure response time
            context.Response.ContentType = "application/octet-stream";
            await context.Response.Body.WriteAsync(data);
            
            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;
            
            // Add performance headers
            context.Response.Headers.Append("X-Protocol", protocol);
            context.Response.Headers.Append("X-Duration-Ms", duration.ToString("F2"));
            context.Response.Headers.Append("X-Data-Size-KB", size.ToString());
            
            return Results.Empty;
        })
        .WithName("Http3PerformanceTest")
        .WithSummary("Test HTTP/3 performance")
        .WithDescription("Downloads test data to measure HTTP/3 performance");

        // POST /api/http3/echo-stream
        http3Group.MapPost("/echo-stream", async (HttpContext context) =>
        {
            var protocol = context.Request.Protocol;
            context.Response.Headers.Append("X-Protocol", protocol);
            
            // Echo back the request body as a stream
            context.Response.ContentType = context.Request.ContentType ?? "application/octet-stream";
            
            await context.Request.Body.CopyToAsync(context.Response.Body);
            
            return Results.Empty;
        })
        .WithName("Http3EchoStream")
        .WithSummary("Echo stream data")
        .WithDescription("Echoes back the posted data using HTTP/3 streaming")
        .Accepts<IFormFile>("multipart/form-data");

        // GET /api/http3/multiplexing-test
        http3Group.MapGet("/multiplexing-test/{streamId}", async (int streamId) =>
        {
            // Simulate different processing times to test multiplexing
            var delay = streamId * 100; // ms
            await Task.Delay(delay);
            
            return Results.Ok(new
            {
                streamId = streamId,
                processingTime = delay,
                timestamp = DateTime.UtcNow,
                message = $"Stream {streamId} completed after {delay}ms"
            });
        })
        .WithName("Http3MultiplexingTest")
        .WithSummary("Test HTTP/3 multiplexing")
        .WithDescription("Tests HTTP/3 multiplexing by simulating different processing times");
    }
}