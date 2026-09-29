namespace DesktopAutomationBot.Runner;

internal sealed class FileLoggingOptions
{
    public bool Enabled { get; init; } = true;

    public string Path { get; init; } = System.IO.Path.Combine("logs", "dabot.jsonl");
}
