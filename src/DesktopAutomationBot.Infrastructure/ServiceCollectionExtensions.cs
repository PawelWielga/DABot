using DesktopAutomationBot.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopAutomationBot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IScenarioLoader, JsonScenarioLoader>();
        services.AddSingleton<IRunStore, SqliteRunStore>();
        services.AddSingleton<IBrowserAutomation, PlaywrightBrowserAutomation>();
        return services;
    }
}
