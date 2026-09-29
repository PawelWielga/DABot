using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DesktopAutomationBot.Runner;

internal sealed class JsonFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();
    private readonly SensitiveValueMasker _masker;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public JsonFileLoggerProvider(string path, SensitiveValueMasker masker)
    {
        _masker = masker;
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _writer = new StreamWriter(
            new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) =>
        new JsonFileLogger(categoryName, _writer, _sync, () => _scopeProvider, _masker);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        _scopeProvider = scopeProvider;

    public void Dispose() => _writer.Dispose();

    private sealed class JsonFileLogger(
        string categoryName,
        StreamWriter writer,
        object sync,
        Func<IExternalScopeProvider> scopeProvider,
        SensitiveValueMasker masker) : ILogger
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
            AddProperties(properties, state, masker);
            scopeProvider().ForEachScope(
                static (scope, target) => AddProperties(target.Properties, scope, target.Masker),
                (Properties: properties, Masker: masker));

            var entry = new
            {
                timestamp = DateTimeOffset.UtcNow,
                level = logLevel.ToString(),
                category = categoryName,
                eventId = eventId.Id,
                message = masker.MaskText(formatter(state, exception)),
                exception = masker.MaskText(exception?.ToString()),
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
            TState state,
            SensitiveValueMasker masker)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values)
            {
                return;
            }

            foreach (var pair in values)
            {
                if (pair.Key != "{OriginalFormat}")
                {
                    target[pair.Key] = masker.MaskValue(pair.Value);
                }
            }
        }
    }
}
