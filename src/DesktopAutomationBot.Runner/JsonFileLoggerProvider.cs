using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Runner;

internal sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

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
        new JsonFileLogger(categoryName, _writer, _sync);

    public void Dispose() => _writer.Dispose();

    private sealed class JsonFileLogger(
        string categoryName,
        StreamWriter writer,
        object sync) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

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

            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values
                    .Where(pair => pair.Key != "{OriginalFormat}")
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

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
    }
}
