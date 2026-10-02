using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DesktopAutomationBot.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IScenarioValidationService, ScenarioValidationService>();
        services.AddSingleton<IBrowserProfileService, BrowserProfileService>();
        services.AddSingleton<IInteractiveBrowserSessionService, InteractiveBrowserSessionService>();
        services.AddSingleton<IScenarioExecutor, ScenarioExecutor>();
        services.AddSingleton<IScenarioTestRunService, ScenarioTestRunService>();
        services.AddSingleton<DurableScenarioExecutor>();
        services.AddSingleton<IDurableScenarioExecutor>(
            static provider => provider.GetRequiredService<DurableScenarioExecutor>());
        services.AddSingleton<IDurableRunResumeService>(
            static provider => provider.GetRequiredService<DurableScenarioExecutor>());
        services.AddSingleton<IDurableRunControlService>(
            static provider => provider.GetRequiredService<DurableScenarioExecutor>());
        services.AddSingleton<IAdministrativeRunControlService, AdministrativeRunControlService>();
        services.AddSingleton<IDurableRunRecoveryService, DurableRunRecoveryService>();
        services.AddSingleton<IDurableRetryScheduler, DurableRetryScheduler>();
        services.AddSingleton<IDurableRetryWorker, DurableRetryWorker>();
        services.AddSingleton<IEventPublisher, EventPublisher>();
        services.AddSingleton<IAdministrativeEventService, AdministrativeEventService>();
        services.AddSingleton<IEventResumeWorker, EventResumeWorker>();
        services.AddSingleton<IPageObserverWorker, PageObserverWorker>();
        services.AddSingleton<IPageObserverManagementService, PageObserverManagementService>();
        services.AddSingleton<INodeRegistryService, NodeRegistryService>();
        services.AddSingleton<IStepHandler, OpenUrlStepHandler>();
        services.AddSingleton<IStepHandler, ClickStepHandler>();
        services.AddSingleton<IStepHandler, FillTextStepHandler>();
        services.AddSingleton<IStepHandler, PasteTextStepHandler>();
        services.AddSingleton<IStepHandler, ReadTextStepHandler>();
        services.AddSingleton<IStepHandler, WaitForStepHandler>();
        services.AddSingleton<IStepHandler, CallApiStepHandler>();
        services.AddSingleton<IStepHandler, DelayStepHandler>();
        services.AddSingleton<IStepHandler, ScreenshotStepHandler>();
        return services;
    }
}
