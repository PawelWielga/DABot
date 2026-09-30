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
builder.Services.AddSingleton<IBrowserProfileCatalog, DemoBrowserProfileCatalog>();
builder.Services.AddSingleton<IBrowserProfileManagementService, DemoBrowserProfileManagementService>();
builder.Services.AddSingleton<IBrowserProfileService, DemoBrowserProfileService>();
builder.Services.AddSingleton<IInteractiveBrowserSessionService, DemoInteractiveBrowserSessionService>();
builder.Services.AddSingleton<IDurableRunControlService, DemoRunControlService>();
builder.Services.AddSingleton<IRunArtifactService, DemoRunArtifactService>();
builder.Services.AddSingleton<IRunQueryService>(
    static provider => provider.GetRequiredService<DemoDataService>());
builder.Services.AddSingleton<IScenarioCatalogQueryService>(
    static provider => provider.GetRequiredService<DemoDataService>());
builder.Services.AddSingleton<IScenarioManagementService>(
    static provider => provider.GetRequiredService<DemoDataService>());

await builder.Build().RunAsync();
