namespace DesktopAutomationBot.Infrastructure;

internal sealed class BrowserProfileLease : IAsyncDisposable, IDisposable
{
    private readonly FileStream _stream;

    private BrowserProfileLease(FileStream stream)
    {
        _stream = stream;
    }

    public static BrowserProfileLease Acquire(string lockFilePath)
    {
        try
        {
            return new BrowserProfileLease(
                new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None));
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Browser profile '{Path.GetFileNameWithoutExtension(lockFilePath)}' is already in use.",
                exception);
        }
    }

    public void Dispose() =>
        _stream.Dispose();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
