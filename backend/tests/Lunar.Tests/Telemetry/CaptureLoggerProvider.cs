using Microsoft.Extensions.Logging;

namespace Lunar.Tests.Telemetry;

/// <summary>
/// A test ILoggerProvider that captures all log entries for assertion.
/// </summary>
public sealed class CaptureLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }


    public ILogger CreateLogger(string categoryName)
    {
        return new CaptureLogger(categoryName, this);
    }


    public ILogger<T> CreateLogger<T>()
    {
        return new CaptureLogger<T>(this);
    }


    internal void AddEntry(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }


    public void Dispose()
    {
    }


    private sealed class CaptureLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly CaptureLoggerProvider _provider;

        public CaptureLogger(string categoryName, CaptureLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }


        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }


        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>();

            if (state is IReadOnlyList<KeyValuePair<string, object?>> stateList)
            {
                foreach (var kvp in stateList)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            _provider.AddEntry(new LogEntry(
                _categoryName,
                logLevel,
                eventId,
                message,
                exception,
                properties));
        }
    }


    private sealed class CaptureLogger<T> : ILogger<T>
    {
        private readonly CaptureLoggerProvider _provider;
        private readonly string _categoryName = typeof(T).FullName ?? typeof(T).Name;

        public CaptureLogger(CaptureLoggerProvider provider)
        {
            _provider = provider;
        }


        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }


        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }


        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>();

            if (state is IReadOnlyList<KeyValuePair<string, object?>> stateList)
            {
                foreach (var kvp in stateList)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            _provider.AddEntry(new LogEntry(
                _categoryName,
                logLevel,
                eventId,
                message,
                exception,
                properties));
        }
    }
}

/// <summary>
/// A captured log entry for test assertions.
/// </summary>
public sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
