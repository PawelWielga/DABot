using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Runner;

internal sealed class JsonFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public JsonFileLoggerProvider(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) =>
        new JsonFileLogger(categoryName, _writer, _sync, () => _scopeProvider);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider;

    public void Dispose() => _writer.Dispose();

    private sealed class JsonFileLogger(
        string categoryName,
        StreamWriter writer,
        object sync,
        Func<IExternalScopeProvider> scopeProvider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopeProvider().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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

            var properties = new Dictionary<string, object?>();
            AddProperties(properties, state);
            scopeProvider().ForEachScope(
                static (scope, target) => AddProperties(target, scope),
                properties);

            var entry = new
            {
                timestamp = DateTimeOffset.UtcNow,
                level = logLevel.ToString(),
                category = categoryName,
                eventId = eventId.Id,
                message = formatter(state, exception),
                exception = exception?.ToString(),
                properties,
            };

            var json = JsonSerializer.Serialize(entry);
            lock (sync)
            {
                writer.WriteLine(json);
            }
        }

        private static void AddProperties<TState>(
            IDictionary<string, object?> target,
            TState state)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            {
                return;
            }

            foreach (var pair in values)
            {
                if (pair.Key != "{OriginalFormat}")
                {
                    target[pair.Key] = pair.Value;
                }
            }
        }
    }
}
