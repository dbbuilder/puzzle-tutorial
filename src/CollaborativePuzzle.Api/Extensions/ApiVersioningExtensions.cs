using Asp.Versioning;
using Asp.Versioning.Builder;

namespace CollaborativePuzzle.Api.Extensions;

/// <summary>
/// Extension methods for configuring API versioning
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning services to the service collection
    /// </summary>
    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-API-Version"),
                new MediaTypeApiVersionReader("ver")
            );
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// Maps versioned endpoints
    /// </summary>
    public static ApiVersionSet CreateApiVersionSet(this WebApplication app)
    {
        return app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .HasApiVersion(new ApiVersion(2, 0))
            .ReportApiVersions()
            .Build();
    }

    /// <summary>
    /// Creates a version set for deprecated v1 endpoints
    /// </summary>
    public static ApiVersionSet CreateDeprecatedApiVersionSet(this WebApplication app)
    {
        return app.NewApiVersionSet()
            .HasDeprecatedApiVersion(new ApiVersion(1, 0))
            .HasApiVersion(new ApiVersion(2, 0))
            .ReportApiVersions()
            .Build();
    }
}