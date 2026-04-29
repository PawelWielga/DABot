using Microsoft.Extensions.DependencyInjection;

namespace DesktopAutomationBot.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IScenarioValidationService, ScenarioValidationService>();
        services.AddSingleton<IScenarioExecutor, ScenarioExecutor>();
        services.AddSingleton<IStepHandler, OpenUrlStepHandler>();
        services.AddSingleton<IStepHandler, ClickStepHandler>();
        services.AddSingleton<IStepHandler, FillTextStepHandler>();
        services.AddSingleton<IStepHandler, PasteTextStepHandler>();
        services.AddSingleton<IStepHandler, ReadTextStepHandler>();
        services.AddSingleton<IStepHandler, WaitForStepHandler>();
        services.AddSingleton<IStepHandler, ScreenshotStepHandler>();
        return services;
    }
}
