using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class FileSystemGeneralRuntimeSettingsService :
    IGeneralRuntimeSettingsService,
    IStorageRuntimeSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _configurationPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemGeneralRuntimeSettingsService(
        string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        _configurationPath = Path.GetFullPath(configurationPath);
    }

    public async Task<GeneralRuntimeSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var root = await ReadRootAsync(cancellationToken);
        var bot = GetNode(root, "bot") as JsonObject;

        if (bot is null)
        {
            return new GeneralRuntimeSettings();
        }

        var options = bot.Deserialize<BotOptions>(SerializerOptions)
            ?? new BotOptions();

        return GeneralRuntimeSettings.FromOptions(options);
    }

    public async Task SaveAsync(
        GeneralRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        GeneralRuntimeSettingsValidator.ValidateOrThrow(settings);

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var root = await ReadRootAsync(cancellationToken);
            var bot = GetOrCreateObject(root, "bot");

            if (string.IsNullOrWhiteSpace(settings.ScenarioPath))
            {
                RemoveNode(bot, "scenarioPath");
            }
            else
            {
                SetNode(
                    bot,
                    "scenarioPath",
                    JsonValue.Create(settings.ScenarioPath.Trim()));
            }

            var browser = GetOrCreateObject(bot, "browser");
            SetBoolean(browser, "headless", settings.BrowserHeadless);
            SetInt32(browser, "slowMoMs", settings.BrowserSlowMoMs);
            SetInt32(browser, "timeoutMs", settings.BrowserTimeoutMs);
            SetInt32(browser, "viewportWidth", settings.BrowserViewportWidth);
            SetInt32(browser, "viewportHeight", settings.BrowserViewportHeight);

            var retryWorker = GetOrCreateObject(bot, "retryWorker");
            SetInt32(
                retryWorker,
                "pollIntervalMs",
                settings.RetryWorkerPollIntervalMs);
            SetInt32(
                retryWorker,
                "batchSize",
                settings.RetryWorkerBatchSize);

            var eventWorker = GetOrCreateObject(bot, "eventWorker");
            SetInt32(
                eventWorker,
                "pollIntervalMs",
                settings.EventWorkerPollIntervalMs);
            SetInt32(
                eventWorker,
                "batchSize",
                settings.EventWorkerBatchSize);
            SetInt32(
                eventWorker,
                "maxAttempts",
                settings.EventWorkerMaxAttempts);
            SetInt32(
                eventWorker,
                "baseRetryDelayMs",
                settings.EventWorkerBaseRetryDelayMs);
            SetInt32(
                eventWorker,
                "maxRetryDelayMs",
                settings.EventWorkerMaxRetryDelayMs);

            var observerWorker = GetOrCreateObject(bot, "observerWorker");
            SetInt32(
                observerWorker,
                "pollIntervalMs",
                settings.ObserverWorkerPollIntervalMs);
            SetInt32(
                observerWorker,
                "batchSize",
                settings.ObserverWorkerBatchSize);
            SetInt32(
                observerWorker,
                "maxErrorBackoffMs",
                settings.ObserverWorkerMaxErrorBackoffMs);

            var interactiveBrowser =
                GetOrCreateObject(bot, "interactiveBrowser");
            SetInt32(
                interactiveBrowser,
                "maxDurationSeconds",
                settings.InteractiveBrowserMaxDurationSeconds);

            await WriteRootAsync(
                root,
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    async Task<StorageRuntimeSettings> IStorageRuntimeSettingsService.GetAsync(
        CancellationToken cancellationToken)
    {
        var root = await ReadRootAsync(cancellationToken);
        var bot = GetNode(root, "bot") as JsonObject;

        if (bot is null)
        {
            return new StorageRuntimeSettings();
        }

        var options = bot.Deserialize<BotOptions>(SerializerOptions)
            ?? new BotOptions();

        return StorageRuntimeSettings.FromOptions(options);
    }

    async Task IStorageRuntimeSettingsService.SaveAsync(
        StorageRuntimeSettings settings,
        CancellationToken cancellationToken)
    {
        StorageRuntimeSettingsValidator.ValidateOrThrow(settings);

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var root = await ReadRootAsync(cancellationToken);
            var bot = GetOrCreateObject(root, "bot");
            var storage = GetOrCreateObject(bot, "storage");

            SetString(
                storage,
                "scenariosDirectory",
                settings.ScenariosDirectory);
            SetString(
                storage,
                "screenshotsDirectory",
                settings.ScreenshotsDirectory);
            SetString(
                storage,
                "artifactsDirectory",
                settings.ArtifactsDirectory);
            SetString(
                storage,
                "browserProfilesDirectory",
                settings.BrowserProfilesDirectory);
            SetString(
                storage,
                "databasePath",
                settings.DatabasePath);

            await WriteRootAsync(
                root,
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<JsonObject> ReadRootAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_configurationPath))
        {
            return new JsonObject();
        }

        var json = await File.ReadAllTextAsync(
            _configurationPath,
            cancellationToken);

        var parsed = JsonNode.Parse(
            json,
            nodeOptions: null,
            documentOptions: DocumentOptions);

        return parsed as JsonObject
            ?? throw new InvalidDataException(
                $"Configuration file '{_configurationPath}' must contain a JSON object.");
    }

    private async Task WriteRootAsync(
        JsonObject root,
        CancellationToken cancellationToken)
    {
        var directory =
            Path.GetDirectoryName(_configurationPath)
            ?? Directory.GetCurrentDirectory();

        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_configurationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var json = root.ToJsonString(SerializerOptions) +
                Environment.NewLine;

            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken);

            File.Move(
                temporaryPath,
                _configurationPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonObject GetOrCreateObject(
        JsonObject parent,
        string propertyName)
    {
        if (GetNode(parent, propertyName) is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        SetNode(parent, propertyName, created);
        return created;
    }

    private static JsonNode? GetNode(
        JsonObject node,
        string propertyName)
    {
        var actualName = FindPropertyName(
            node,
            propertyName);

        return actualName is null
            ? null
            : node[actualName];
    }

    private static void SetString(
        JsonObject node,
        string propertyName,
        string value) =>
        SetNode(
            node,
            propertyName,
            JsonValue.Create(value.Trim()));

    private static void SetBoolean(
        JsonObject node,
        string propertyName,
        bool value) =>
        SetNode(
            node,
            propertyName,
            JsonValue.Create(value));

    private static void SetInt32(
        JsonObject node,
        string propertyName,
        int value) =>
        SetNode(
            node,
            propertyName,
            JsonValue.Create(value));

    private static void SetNode(
        JsonObject node,
        string propertyName,
        JsonNode? value)
    {
        var actualName = FindPropertyName(
            node,
            propertyName);

        node[actualName ?? propertyName] = value;
    }

    private static void RemoveNode(
        JsonObject node,
        string propertyName)
    {
        var actualName = FindPropertyName(
            node,
            propertyName);

        if (actualName is not null)
        {
            node.Remove(actualName);
        }
    }

    private static string? FindPropertyName(
        JsonObject node,
        string propertyName) =>
        node
            .Select(pair => pair.Key)
            .FirstOrDefault(
                key => string.Equals(
                    key,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase));
}
