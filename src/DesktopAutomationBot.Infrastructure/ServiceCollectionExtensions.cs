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
        services.AddSingleton<IEventInboxStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IPageObserverStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IPageObserverManagementStore>(
            static provider => provider.GetRequiredService<SqliteRunStore>());
        services.AddSingleton<IRunQueryService, SqliteRunQueryService>();
        services.AddSingleton<IEventHistoryQueryService, SqliteEventHistoryQueryService>();
        services.AddSingleton<IPageObserverQueryService, SqlitePageObserverQueryService>();
        services.AddSingleton<IRunArtifactService, FileSystemRunArtifactService>();
        services.AddSingleton<IScenarioCatalogQueryService, FileSystemScenarioCatalogQueryService>();
        services.AddSingleton<IScenarioManagementService, FileSystemScenarioManagementService>();
        services.AddSingleton<IBrowserProfileCatalog, FileSystemBrowserProfileCatalog>();
        services.AddSingleton<IBrowserProfileManagementService, FileSystemBrowserProfileManagementService>();
        services.AddSingleton<IInteractiveBrowserSessionAuditSink, LoggingInteractiveBrowserSessionAuditSink>();
        services.AddSingleton<IAdministrativeAuditSink, LoggingAdministrativeAuditSink>();
        services.AddSingleton<INodeIdentityProvider, FileSystemNodeIdentityProvider>();
        services.AddSingleton<IHttpAutomationClient, HttpAutomationClient>();
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<IBrowserSessionFactory, PlaywrightBrowserSessionFactory>();
        return services;
    }
}
