using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

namespace CollaborativePuzzle.Api.MinimalApis;

public static class SimpleMinimalApiEndpoints
{
    public static void ConfigureMinimalApis(this WebApplicationBuilder builder)
    {
        // Add OpenAPI/Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Collaborative Puzzle API",
                Version = "v1",
                Description = "REST API for the Collaborative Puzzle Platform with WebSocket support"
            });
        });

        // Add simple rate limiting
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.AutoReplenishment = true;
            });
        });
    }

    public static void MapMinimalApis(this WebApplication app)
    {
        // Map health endpoints
        app.MapHealthEndpoints();

        // Map demo endpoints
        app.MapDemoEndpoints();

        // Add Swagger UI
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Collaborative Puzzle API v1");
                options.RoutePrefix = "api-docs";
            });

            // Redirect root to Swagger UI
            app.MapGet("/", () => Results.Redirect("/api-docs"))
                .ExcludeFromDescription();
        }
    }

    private static void MapDemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/demo")
            .WithTags("Demo")
            .WithOpenApi()
            .RequireRateLimiting("fixed");

        // GET /api/demo/status
        group.MapGet("/status", () =>
        {
            return Results.Ok(new
            {
                status = "running",
                version = "1.0.0",
                timestamp = DateTime.UtcNow,
                features = new[]
                {
                    "SignalR Hub",
                    "WebSocket Raw",
                    "WebRTC Signaling",
                    "MQTT Integration",
                    "Socket.IO Compatibility",
                    "Kubernetes Ready"
                }
            });
        })
        .WithName("GetApiStatus")
        .WithSummary("Get API status")
        .WithDescription("Returns the current API status and available features")
        .Produces<object>(StatusCodes.Status200OK);

        // GET /api/demo/connections
        group.MapGet("/connections", () =>
        {
            return Results.Ok(new
            {
                endpoints = new[]
                {
                    new { type = "SignalR", url = "/puzzlehub", protocol = "WebSocket" },
                    new { type = "WebRTC", url = "/webrtchub", protocol = "WebSocket" },
                    new { type = "Raw WebSocket", url = "/ws", protocol = "WebSocket" },
                    new { type = "Socket.IO", url = "/socket.io", protocol = "WebSocket" },
                    new { type = "MQTT", url = "ws://localhost:9001", protocol = "MQTT over WebSocket" }
                }
            });
        })
        .WithName("GetConnectionEndpoints")
        .WithSummary("Get available connection endpoints")
        .WithDescription("Returns all available real-time connection endpoints")
        .Produces<object>(StatusCodes.Status200OK);

        // POST /api/demo/echo
        group.MapPost("/echo", (EchoRequest request) =>
        {
            return Results.Ok(new
            {
                message = request.Message,
                timestamp = DateTime.UtcNow,
                echoedAt = DateTime.UtcNow.ToString("O")
            });
        })
        .WithName("EchoMessage")
        .WithSummary("Echo a message")
        .WithDescription("Simple echo endpoint for testing")
        .Produces<object>(StatusCodes.Status200OK);

        // GET /api/demo/puzzle-sample
        group.MapGet("/puzzle-sample", () =>
        {
            return Results.Ok(new
            {
                id = Guid.NewGuid(),
                name = "Sample Puzzle",
                description = "A demonstration puzzle",
                pieceCount = 100,
                difficulty = 3,
                imageUrl = "/images/sample-puzzle.jpg",
                createdAt = DateTime.UtcNow
            });
        })
        .WithName("GetSamplePuzzle")
        .WithSummary("Get a sample puzzle")
        .WithDescription("Returns a sample puzzle object for demonstration")
        .Produces<object>(StatusCodes.Status200OK);
    }
}

// Simple request DTO
public record EchoRequest(string Message);