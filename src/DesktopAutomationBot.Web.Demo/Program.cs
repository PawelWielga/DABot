using DesktopAutomationBot.Application;
using DesktopAutomationBot.Web.Demo;
using DesktopAutomationBot.Web.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(
    new ManagementUiEnvironment(
        RuntimeLabel: "Demo data",
        IsDemo: true));
builder.Services.AddSingleton<DemoDataService>();
builder.Services.AddSingleton<IAdministrativeEventService, DemoAdministrativeEventService>();
builder.Services.AddSingleton<IEventHistoryQueryService, DemoEventHistoryQueryService>();
builder.Services.AddSingleton<IGeneralRuntimeSettingsService, DemoGeneralRuntimeSettingsService>();
builder.Services.AddSingleton<IStorageRuntimeSettingsService, DemoStorageRuntimeSettingsService>();
builder.Services.AddSingleton<DemoPageObserverQueryService>();
builder.Services.AddSingleton<IPageObserverQueryService>(
    static provider => provider.GetRequiredService<DemoPageObserverQueryService>());
builder.Services.AddSingleton<IPageObserverManagementStore>(
    static provider => provider.GetRequiredService<DemoPageObserverQueryService>());
builder.Services.AddSingleton<IPageObserverManagementService, PageObserverManagementService>();
builder.Services.AddSingleton<IBrowserProfileCatalog, DemoBrowserProfileCatalog>();
builder.Services.AddSingleton<IBrowserProfileManagementService, DemoBrowserProfileManagementService>();
builder.Services.AddSingleton<IBrowserProfileService, DemoBrowserProfileService>();
builder.Services.AddSingleton<IInteractiveBrowserSessionService, DemoInteractiveBrowserSessionService>();
builder.Services.AddSingleton<DemoRunControlService>();
builder.Services.AddSingleton<IDurableRunControlService>(
    static provider => provider.GetRequiredService<DemoRunControlService>());
builder.Services.AddSingleton<IAdministrativeRunControlService>(
    static provider => provider.GetRequiredService<DemoRunControlService>());
builder.Services.AddSingleton<IScenarioTestRunService, DemoScenarioTestRunService>();
builder.Services.AddSingleton<IRunArtifactService, DemoRunArtifactService>();
builder.Services.AddSingleton<IRunQueryService>(
    static provider => provider.GetRequiredService<DemoDataService>());
builder.Services.AddSingleton<IScenarioCatalogQueryService>(
    static provider => provider.GetRequiredService<DemoDataService>());
builder.Services.AddSingleton<IScenarioManagementService>(
    static provider => provider.GetRequiredService<DemoDataService>());

await builder.Build().RunAsync();
