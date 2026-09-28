namespace DesktopAutomationBot.Application;

public interface IDurableRetryWorker
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
