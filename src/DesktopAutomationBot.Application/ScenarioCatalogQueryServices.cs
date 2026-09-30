namespace DesktopAutomationBot.Application;

public interface IScenarioCatalogQueryService
{
    Task<IReadOnlyList<ScenarioListItem>> ListAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ScenarioListItem(
    string FileName,
    string Name,
    int SchemaVersion,
    int StepCount,
    string? BrowserProfile,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors);
