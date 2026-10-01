using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using DesktopAutomationBot.Web.Components;
using DesktopAutomationBot.Web.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
    });
builder.Services.AddAuthorization();

builder.Services.Configure<BotOptions>(
    builder.Configuration.GetSection("bot"));
builder.Services.AddSingleton(
    static provider =>
        provider.GetRequiredService<IOptions<BotOptions>>().Value);
builder.Services.AddSingleton(
    new ManagementUiEnvironment(
        RuntimeLabel: "Local runtime"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var runtimeSettingsService =
    new FileSystemGeneralRuntimeSettingsService(
        Path.Combine(
            builder.Environment.ContentRootPath,
            "appsettings.json"));

builder.Services.AddSingleton<IGeneralRuntimeSettingsService>(
    runtimeSettingsService);
builder.Services.AddSingleton<IStorageRuntimeSettingsService>(
    runtimeSettingsService);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet(
    "/api/runs/{runId:guid}/artifacts/{source}/{fileName}",
    async (
        Guid runId,
        string source,
        string fileName,
        IRunArtifactService artifacts,
        CancellationToken cancellationToken) =>
    {
        if (!Enum.TryParse<RunArtifactSource>(
                source,
                ignoreCase: true,
                out var artifactSource))
        {
            return Results.NotFound();
        }

        RunArtifactContent? artifact;

        try
        {
            artifact = await artifacts.OpenAsync(
                runId,
                artifactSource,
                fileName,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }

        if (artifact is null)
        {
            return Results.NotFound();
        }

        return Results.File(
            artifact.Content,
            artifact.ContentType,
            fileDownloadName: artifact.Inline ? null : artifact.FileName,
            enableRangeProcessing: true);
    });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
