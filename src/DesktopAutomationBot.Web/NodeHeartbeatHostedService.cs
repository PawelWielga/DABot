using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Web;

public sealed class NodeHeartbeatHostedService(
    INodeHeartbeatService heartbeat) : BackgroundService
{
    protected override Task ExecuteAsync(
        CancellationToken stoppingToken) =>
        heartbeat.RunAsync(stoppingToken);
}
