using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Infrastructure.Data;
using CollaborativePuzzle.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CollaborativePuzzle.Tests.TestHelpers;

public static class ServiceConfiguration
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Jwt:SecretKey", "ThisIsATestSecretKeyThatIsAtLeast256BitsLong123456" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpirationInMinutes", "60" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Add EF Core with in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Add services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}