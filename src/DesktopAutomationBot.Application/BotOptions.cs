namespace DesktopAutomationBot.Application;

public sealed class BotOptions
{
    public string? ScenarioPath { get; init; }

    public BrowserOptions Browser { get; init; } = new();

    public StorageOptions Storage { get; init; } = new();

    public RetryWorkerOptions RetryWorker { get; init; } = new();

    public EventWorkerOptions EventWorker { get; init; } = new();

    public ObserverWorkerOptions ObserverWorker { get; init; } = new();

    public InteractiveBrowserOptions InteractiveBrowser { get; init; } = new();

    public NodeOptions Node { get; init; } = new();
}

public sealed class NodeOptions
{
    public string IdentityPath { get; init; } =
        Path.Combine("data", "node-id");

    public string? DisplayName { get; init; }

    public string[] Tags { get; init; } = [];

    public string[] Capabilities { get; init; } = [];

    public int ExecutionSlots { get; init; } = 1;
}

public sealed class InteractiveBrowserOptions
{
    public int MaxDurationSeconds { get; init; } = 1800;
}

public sealed class ObserverWorkerOptions
{
    public int PollIntervalMs { get; init; } = 1000;

    public int BatchSize { get; init; } = 50;

    public int MaxErrorBackoffMs { get; init; } = 60_000;
}

public sealed class EventWorkerOptions
{
    public int PollIntervalMs { get; init; } = 1000;

    public int BatchSize { get; init; } = 100;

    public int MaxAttempts { get; init; } = 5;

    public int BaseRetryDelayMs { get; init; } = 1000;

    public int MaxRetryDelayMs { get; init; } = 60_000;
}

public sealed class RetryWorkerOptions
{
    public int PollIntervalMs { get; init; } = 1000;

    public int BatchSize { get; init; } = 100;
}

public sealed class BrowserOptions
{
    public bool Headless { get; init; } = true;

    public int SlowMoMs { get; init; }

    public int TimeoutMs { get; init; } = 30_000;

    public int ViewportWidth { get; init; } = 1280;

    public int ViewportHeight { get; init; } = 720;
}

public sealed class StorageOptions
{
    public string ScenariosDirectory { get; init; } = Path.Combine("scenarios");

    public string ScreenshotsDirectory { get; init; } = Path.Combine("screenshots");

    public string ArtifactsDirectory { get; init; } = Path.Combine("artifacts");

    public string BrowserProfilesDirectory { get; init; } =
        Path.Combine("data", "browser-profiles");

    public string DatabasePath { get; init; } = Path.Combine("data", "dabot.db");
}
