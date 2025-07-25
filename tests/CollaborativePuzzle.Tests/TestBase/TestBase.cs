using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CollaborativePuzzle.Tests.TestBase
{
    /// <summary>
    /// Base class for all unit tests providing common functionality and test helpers.
    /// Implements IAsyncLifetime for async setup/teardown support.
    /// </summary>
    public abstract class TestBase : IAsyncLifetime
    {
        /// <summary>
        /// Gets the xUnit test output helper for logging test execution details.
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// Gets the service collection for dependency injection setup.
        /// </summary>
        protected IServiceCollection Services { get; }

        /// <summary>
        /// Gets the built service provider after all services are registered.
        /// </summary>
        protected IServiceProvider ServiceProvider { get; private set; } = null!;

        /// <summary>
        /// Gets the mock repository for creating and verifying mocks.
        /// </summary>
        protected MockRepository MockRepository { get; }

        /// <summary>
        /// Gets the test logger that writes to xUnit output.
        /// </summary>
        protected ILogger<TestBase> Logger { get; private set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestBase"/> class.
        /// </summary>
        /// <param name="output">The xUnit test output helper.</param>
        protected TestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Services = new ServiceCollection();
            MockRepository = new MockRepository(MockBehavior.Strict);
            
            // Add xUnit logging
            Services.AddLogging(builder =>
            {
                builder.AddXUnit(output);
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        }

        /// <summary>
        /// Initializes the test asynchronously. Override to add custom initialization.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            // Build service provider
            ServiceProvider = Services.BuildServiceProvider();
            Logger = ServiceProvider.GetRequiredService<ILogger<TestBase>>();
            
            Logger.LogDebug("Initializing test: {TestName}", GetType().Name);
            
            // Allow derived classes to perform async initialization
            await OnInitializeAsync();
        }

        /// <summary>
        /// Disposes the test asynchronously. Override to add custom cleanup.
        /// </summary>
        public virtual async Task DisposeAsync()
        {
            Logger.LogDebug("Disposing test: {TestName}", GetType().Name);
            
            // Allow derived classes to perform async cleanup
            await OnDisposeAsync();
            
            // Verify all mocks were called as expected
            try
            {
                MockRepository.VerifyAll();
            }
            catch (MockException ex)
            {
                Logger.LogError(ex, "Mock verification failed");
                throw;
            }
            
            // Dispose service provider
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Override to perform custom async initialization.
        /// </summary>
        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        /// <summary>
        /// Override to perform custom async cleanup.
        /// </summary>
        protected virtual Task OnDisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Creates a mock of the specified type using the test's mock repository.
        /// </summary>
        /// <typeparam name="T">The type to mock.</typeparam>
        /// <returns>A mock instance.</returns>
        protected Mock<T> CreateMock<T>() where T : class
        {
            var mock = MockRepository.Create<T>();
            Logger.LogDebug("Created mock for type: {Type}", typeof(T).Name);
            return mock;
        }

        /// <summary>
        /// Gets a service from the dependency injection container.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The resolved service.</returns>
        protected T GetService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Logs a test step for better debugging and understanding test flow.
        /// </summary>
        /// <param name="step">The step description.</param>
        protected void LogTestStep(string step)
        {
            Logger.LogInformation("Test Step: {Step}", step);
            Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {step}");
        }

        /// <summary>
        /// Creates test data with a unique identifier to avoid conflicts.
        /// </summary>
        /// <param name="prefix">The prefix for the test data.</param>
        /// <returns>A unique test data string.</returns>
        protected string CreateTestData(string prefix = "test")
        {
            return $"{prefix}_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Executes an action and captures any exception for assertion.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The captured exception or null.</returns>
        protected Exception? CaptureException(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Captured expected exception");
                return ex;
            }
        }

        /// <summary>
        /// Executes an async function and captures any exception for assertion.
        /// </summary>
        /// <param name="func">The async function to execute.</param>
        /// <returns>The captured exception or null.</returns>
        protected async Task<Exception?> CaptureExceptionAsync(Func<Task> func)
        {
            try
            {
                await func();
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Captured expected exception");
                return ex;
            }
        }

        /// <summary>
        /// Asserts that two collections are equivalent (same elements, any order).
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="expected">The expected collection.</param>
        /// <param name="actual">The actual collection.</param>
        protected void AssertCollectionsEquivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            Assert.Equal(
                expected.OrderBy(x => x?.GetHashCode()), 
                actual.OrderBy(x => x?.GetHashCode())
            );
        }

        /// <summary>
        /// Creates a cancellation token that expires after the specified timeout.
        /// </summary>
        /// <param name="timeout">The timeout duration.</param>
        /// <returns>A cancellation token.</returns>
        protected CancellationToken CreateTimeout(TimeSpan? timeout = null)
        {
            var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
            return cts.Token;
        }
    }
}