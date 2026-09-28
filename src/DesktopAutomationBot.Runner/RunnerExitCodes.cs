namespace DesktopAutomationBot.Runner;

public static class RunnerExitCodes
{
    public const int Success = 0;
    public const int UnexpectedError = 1;
    public const int UsageError = 2;
    public const int InputError = 3;
    public const int ExecutionFailed = 4;
    public const int Cancelled = 130;
}
