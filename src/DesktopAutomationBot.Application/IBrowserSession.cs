namespace DesktopAutomationBot.Application;

public interface IBrowserSession : IBrowserAutomation
{
}

public interface IBrowserSessionFactory
{
    ValueTask<IBrowserSession> CreateAsync(
        CancellationToken cancellationToken = default);
}
