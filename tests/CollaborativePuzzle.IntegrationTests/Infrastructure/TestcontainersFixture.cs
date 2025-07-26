using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace CollaborativePuzzle.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture that manages Docker containers for integration tests
/// </summary>
public class TestcontainersFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private readonly RedisContainer _redisContainer;
    
    public string SqlConnectionString => _sqlContainer.GetConnectionString();
    public string RedisConnectionString => _redisContainer.GetConnectionString();
    
    public TestcontainersFixture()
    {
        // Configure SQL Server container
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Test@123456!")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();
            
        // Configure Redis container
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();
    }
    
    public async Task InitializeAsync()
    {
        // Start containers in parallel
        await Task.WhenAll(
            _sqlContainer.StartAsync(),
            _redisContainer.StartAsync()
        );
        
        // Initialize database schema
        await InitializeDatabaseAsync();
    }
    
    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _sqlContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask()
        );
    }
    
    private async Task InitializeDatabaseAsync()
    {
        // This will be implemented to run migrations or seed data
        await Task.CompletedTask;
    }
}

/// <summary>
/// Base class for integration tests with container support
/// </summary>
public class IntegrationTestBase : IClassFixture<TestcontainersFixture>
{
    protected readonly TestcontainersFixture Fixture;
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    
    public IntegrationTestBase(TestcontainersFixture fixture)
    {
        Fixture = fixture;
        
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override connection strings with test containers
                    services.Configure<ConnectionStrings>(options =>
                    {
                        options.DefaultConnection = fixture.SqlConnectionString;
                        options.Redis = fixture.RedisConnectionString;
                    });
                    
                    // Disable authentication for testing
                    services.AddAuthentication("Test")
                        .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
                            "Test", options => { });
                    
                    // Reduce logging noise
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Warning);
                    });
                });
                
                builder.UseEnvironment("Testing");
            });
            
        Client = Factory.CreateClient();
    }
}

// Connection strings configuration class
public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
    public string Redis { get; set; } = string.Empty;
}