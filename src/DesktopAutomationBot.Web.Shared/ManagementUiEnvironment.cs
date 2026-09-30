namespace DesktopAutomationBot.Web.Shared;

public sealed record ManagementUiEnvironment(
    string RuntimeLabel,
    bool IsDemo = false);
