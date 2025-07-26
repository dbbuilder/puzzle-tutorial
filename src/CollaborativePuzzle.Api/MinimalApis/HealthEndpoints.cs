using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollaborativePuzzle.Api.MinimalApis;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Basic health check
        endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .WithSummary("Basic health check")
            .WithDescription("Returns a simple health status")
            .WithTags("Health")
            .Produces<object>(StatusCodes.Status200OK)
            .AllowAnonymous();

        // Detailed health check
        endpoints.MapHealthChecks("/health/detailed", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse,
            AllowCachingResponses = false
        })
        .WithName("DetailedHealthCheck")
        .WithSummary("Detailed health check")
        .WithDescription("Returns detailed health status including all dependencies")
        .WithTags("Health")
        .AllowAnonymous();

        // Liveness probe (for Kubernetes)
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "Alive", timestamp = DateTime.UtcNow }))
            .WithName("LivenessCheck")
            .WithSummary("Liveness probe")
            .WithDescription("Kubernetes liveness probe endpoint")
            .WithTags("Health")
            .Produces<object>(StatusCodes.Status200OK)
            .AllowAnonymous()
            .ExcludeFromDescription(); // Hide from OpenAPI

        // Readiness probe (for Kubernetes)
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponse
        })
        .WithName("ReadinessCheck")
        .WithSummary("Readiness probe")
        .WithDescription("Kubernetes readiness probe endpoint")
        .WithTags("Health")
        .AllowAnonymous()
        .ExcludeFromDescription(); // Hide from OpenAPI

        // Startup probe (for Kubernetes)
        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = WriteHealthCheckResponse
        })
        .WithName("StartupCheck")
        .WithSummary("Startup probe")
        .WithDescription("Kubernetes startup probe endpoint")
        .WithTags("Health")
        .AllowAnonymous()
        .ExcludeFromDescription(); // Hide from OpenAPI
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration,
            Timestamp = DateTime.UtcNow,
            Results = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthCheckResult
                {
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    Duration = entry.Value.Duration,
                    Exception = entry.Value.Exception?.Message,
                    Data = entry.Value.Data.Count > 0 ? entry.Value.Data : null
                })
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

// Health check response models
public class HealthCheckResponse
{
    public string Status { get; set; } = default!;
    public TimeSpan TotalDuration { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, HealthCheckResult> Results { get; set; } = default!;
}

public class HealthCheckResult
{
    public string Status { get; set; } = default!;
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Exception { get; set; }
    public IReadOnlyDictionary<string, object>? Data { get; set; }
}