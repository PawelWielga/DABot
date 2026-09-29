using DesktopAutomationBot.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopAutomationBot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IScenarioLoader, JsonScenarioLoader>();
        services.AddSingleton<SqliteRunStore>();
        services.AddSingleton<IRunStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IStepAttemptStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IRetryRunStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IHttpAutomationClient, HttpAutomationClient>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<IBrowserAutomation, PlaywrightBrowserAutomation>();
        return services;
    }
}
