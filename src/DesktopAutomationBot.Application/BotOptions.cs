namespace DesktopAutomationBot.Application;

public sealed class BotOptions
{
    public string? ScenarioPath { get; init; }

    public BrowserOptions Browser { get; init; } = new();

    public StorageOptions Storage { get; init; } = new();

    public RetryWorkerOptions RetryWorker { get; init; } = new();

    public EventWorkerOptions EventWorker { get; init; } = new();
}

public sealed class EventWorkerOptions
{
    public int PollIntervalMs { get; init; } = 1000;

    public int BatchSize { get; init; } = 100;
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
