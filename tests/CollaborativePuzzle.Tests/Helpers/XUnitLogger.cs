using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CollaborativePuzzle.Tests.Helpers
{
    /// <summary>
    /// Logger implementation that writes to xUnit test output.
    /// Enables viewing logs during test execution for better debugging.
    /// </summary>
    public class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="XUnitLogger"/> class.
        /// </summary>
        /// <param name="output">The xUnit test output helper.</param>
        /// <param name="categoryName">The logger category name.</param>
        /// <param name="minLevel">The minimum log level to output.</param>
        public XUnitLogger(ITestOutputHelper output, string categoryName, LogLevel minLevel = LogLevel.Debug)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _minLevel = minLevel;
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return new LogScope();
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }

            var logLevelString = GetLogLevelString(logLevel);
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            
            var formattedMessage = $"[{timestamp}] [{logLevelString}] {_categoryName} - {message}";
            
            if (exception != null)
            {
                formattedMessage += Environment.NewLine + exception;
            }

            try
            {
                _output.WriteLine(formattedMessage);
            }
            catch (InvalidOperationException)
            {
                // Test has already finished, ignore
            }
        }

        /// <summary>
        /// Gets a short string representation of the log level.
        /// </summary>
        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRCE",
                LogLevel.Debug => "DBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERRO",
                LogLevel.Critical => "CRIT",
                LogLevel.None => "NONE",
                _ => logLevel.ToString().ToUpper()
            };
        }

        /// <summary>
        /// Represents a logging scope that does nothing.
        /// </summary>
        private class LogScope : IDisposable
        {
            public void Dispose()
            {
                // Nothing to dispose
            }
        }
    }

    /// <summary>
    /// Logger provider for xUnit test output.
    /// </summary>
    public class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        private readonly LogLevel _minLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="XUnitLoggerProvider"/> class.
        /// </summary>
        /// <param name="output">The xUnit test output helper.</param>
        /// <param name="minLevel">The minimum log level to output.</param>
        public XUnitLoggerProvider(ITestOutputHelper output, LogLevel minLevel = LogLevel.Debug)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _minLevel = minLevel;
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new XUnitLogger(_output, categoryName, _minLevel);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Extension methods for configuring xUnit logging.
    /// </summary>
    public static class XUnitLoggerExtensions
    {
        /// <summary>
        /// Adds xUnit test output logging to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="output">The xUnit test output helper.</param>
        /// <param name="minLevel">The minimum log level to output.</param>
        /// <returns>The logging builder.</returns>
        public static ILoggingBuilder AddXUnit(
            this ILoggingBuilder builder,
            ITestOutputHelper output,
            LogLevel minLevel = LogLevel.Debug)
        {
            builder.AddProvider(new XUnitLoggerProvider(output, minLevel));
            return builder;
        }
    }
}