using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CollaborativePuzzle.Infrastructure.Data;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;
using Xunit.Abstractions;

namespace CollaborativePuzzle.Tests.TestBase
{
    /// <summary>
    /// Base class for integration tests that require a full application host with real services.
    /// Uses TestContainers for SQL Server and Redis to ensure isolated test environments.
    /// </summary>
    public abstract class IntegrationTestBase : TestBase, IClassFixture<IntegrationTestBase.TestApplicationFactory>
    {
        /// <summary>
        /// Gets the test application factory for creating test servers.
        /// </summary>
        protected TestApplicationFactory Factory { get; }

        /// <summary>
        /// Gets the HTTP client configured for the test server.
        /// </summary>
        protected HttpClient Client { get; private set; } = null!;

        /// <summary>
        /// Gets the database context for test data setup and verification.
        /// </summary>
        protected PuzzleDbContext DbContext { get; private set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationTestBase"/> class.
        /// </summary>
        /// <param name="output">The xUnit test output helper.</param>
        /// <param name="factory">The test application factory.</param>
        protected IntegrationTestBase(ITestOutputHelper output, TestApplicationFactory factory) 
            : base(output)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Initializes the integration test with database setup.
        /// </summary>
        protected override async Task OnInitializeAsync()
        {
            await base.OnInitializeAsync();
            
            LogTestStep("Setting up integration test environment");
            
            // Create HTTP client
            Client = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Allow derived tests to configure services
                    ConfigureTestServices(services);
                });
            }).CreateClient();
            
            // Get database context
            var scope = Factory.Services.CreateScope();
            DbContext = scope.ServiceProvider.GetRequiredService<PuzzleDbContext>();
            
            // Ensure database is created and migrated
            await DbContext.Database.MigrateAsync();
            
            // Seed test data
            await SeedTestDataAsync();
        }

        /// <summary>
        /// Cleans up the integration test environment.
        /// </summary>
        protected override async Task OnDisposeAsync()
        {
            LogTestStep("Cleaning up integration test environment");
            
            // Clean up test data
            await CleanupTestDataAsync();
            
            Client?.Dispose();
            DbContext?.Dispose();
            
            await base.OnDisposeAsync();
        }

        /// <summary>
        /// Override to configure additional test services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        protected virtual void ConfigureTestServices(IServiceCollection services)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override to seed test data before each test.
        /// </summary>
        protected virtual Task SeedTestDataAsync() => Task.CompletedTask;

        /// <summary>
        /// Override to clean up test data after each test.
        /// </summary>
        protected virtual Task CleanupTestDataAsync() => Task.CompletedTask;

        /// <summary>
        /// Executes a database transaction and rolls it back for test isolation.
        /// </summary>
        /// <param name="action">The action to execute within the transaction.</param>
        protected async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            using var transaction = await DbContext.Database.BeginTransactionAsync();
            try
            {
                await action();
                // Rollback to ensure test isolation
                await transaction.RollbackAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Custom WebApplicationFactory that uses TestContainers for dependencies.
        /// </summary>
        public class TestApplicationFactory : WebApplicationFactory<Program>
        {
            private MsSqlContainer? _sqlContainer;
            private RedisContainer? _redisContainer;
            
            /// <summary>
            /// Gets the SQL Server connection string from the test container.
            /// </summary>
            public string SqlConnectionString => _sqlContainer?.GetConnectionString() 
                ?? throw new InvalidOperationException("SQL container not started");
            
            /// <summary>
            /// Gets the Redis connection string from the test container.
            /// </summary>
            public string RedisConnectionString => _redisContainer?.GetConnectionString() 
                ?? throw new InvalidOperationException("Redis container not started");

            /// <summary>
            /// Configures the web host for testing.
            /// </summary>
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureServices(async services =>
                {
                    // Start test containers
                    await StartContainersAsync();
                    
                    // Replace SQL Server connection
                    services.RemoveAll<DbContextOptions<PuzzleDbContext>>();
                    services.AddDbContext<PuzzleDbContext>(options =>
                    {
                        options.UseSqlServer(SqlConnectionString);
                    });
                    
                    // Replace Redis connection
                    services.Configure<StackExchange.Redis.ConfigurationOptions>(options =>
                    {
                        options.EndPoints.Clear();
                        options.EndPoints.Add(RedisConnectionString);
                    });
                    
                    // Use in-memory SignalR for testing
                    services.AddSignalR()
                        .AddMessagePackProtocol();
                    
                    // Reduce logging noise in tests
                    services.AddLogging(builder =>
                    {
                        builder.SetMinimumLevel(LogLevel.Warning);
                    });
                });
                
                builder.UseEnvironment("Testing");
            }

            /// <summary>
            /// Starts the test containers.
            /// </summary>
            private async Task StartContainersAsync()
            {
                if (_sqlContainer == null)
                {
                    _sqlContainer = new MsSqlBuilder()
                        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                        .WithPassword("Test@123!")
                        .Build();
                    
                    await _sqlContainer.StartAsync();
                }
                
                if (_redisContainer == null)
                {
                    _redisContainer = new RedisBuilder()
                        .WithImage("redis:7-alpine")
                        .Build();
                    
                    await _redisContainer.StartAsync();
                }
            }

            /// <summary>
            /// Disposes the test containers.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sqlContainer?.DisposeAsync().AsTask().Wait();
                    _redisContainer?.DisposeAsync().AsTask().Wait();
                }
                
                base.Dispose(disposing);
            }

            /// <summary>
            /// Creates a new instance of the application for testing.
            /// </summary>
            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureServices(services =>
                {
                    // Ensure containers are started before building the host
                    StartContainersAsync().Wait();
                });
                
                return base.CreateHost(builder);
            }
        }
    }
}