using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using DesktopAutomationBot.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace DesktopAutomationBot.Infrastructure;

public sealed class RuntimeNodeMetadataProvider(
    BotOptions options,
    ILogger<RuntimeNodeMetadataProvider> logger) : INodeMetadataProvider
{
    public async Task<NodeMetadata> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var browserVersions =
            await DetectBrowserVersionsAsync(cancellationToken);

        var capabilities = new List<string>(
            options.Node.Capabilities ?? []);

        capabilities.Add(GetPlatformCapability());
        capabilities.Add("interactive");

        if (browserVersions.ContainsKey("chromium"))
        {
            capabilities.Add("chromium");
        }

        return NodeMetadata.Validate(
            new NodeMetadata
            {
                DisplayName =
                    string.IsNullOrWhiteSpace(options.Node.DisplayName)
                        ? Environment.MachineName
                        : options.Node.DisplayName,
                OperatingSystem =
                    $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.ProcessArchitecture})",
                DABotVersion = GetDABotVersion(),
                BrowserVersions = browserVersions,
                Tags = options.Node.Tags ?? [],
                Capabilities = capabilities,
                ExecutionSlots = options.Node.ExecutionSlots,
            });
    }

    private async Task<IReadOnlyDictionary<string, string>>
        DetectBrowserVersionsAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            using var playwright =
                await Playwright.CreateAsync()
                    .WaitAsync(cancellationToken);

            var executablePath =
                playwright.Chromium.ExecutablePath;

            if (string.IsNullOrWhiteSpace(executablePath) ||
                !File.Exists(executablePath))
            {
                return EmptyVersions();
            }

            var version =
                await TryReadBrowserVersionAsync(
                    executablePath,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(version))
            {
                return EmptyVersions();
            }

            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["chromium"] = version,
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Could not detect installed browser versions for node metadata.");

            return EmptyVersions();
        }
    }

    private async Task<string?> TryReadBrowserVersionAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.StartInfo.ArgumentList.Add("--version");

        try
        {
            if (!process.Start())
            {
                return null;
            }

            using var timeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var outputTask =
                process.StandardOutput.ReadToEndAsync(
                    timeout.Token);
            var errorTask =
                process.StandardError.ReadToEndAsync(
                    timeout.Token);

            await process.WaitForExitAsync(timeout.Token);

            var output = (
                await outputTask)
                .Trim();

            if (string.IsNullOrWhiteSpace(output))
            {
                output = (
                    await errorTask)
                    .Trim();
            }

            if (process.ExitCode != 0 ||
                string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var separator = output.IndexOf(' ');
            return separator >= 0 &&
                   separator < output.Length - 1
                ? output[(separator + 1)..].Trim()
                : output;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);

            logger.LogDebug(
                "Timed out while probing browser version from {ExecutablePath}.",
                executablePath);

            return null;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception exception)
        {
            TryKill(process);

            logger.LogDebug(
                exception,
                "Could not probe browser version from {ExecutablePath}.",
                executablePath);

            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only. Browser version detection must never make
            // node registration fail.
        }
    }

    private static string GetDABotVersion()
    {
        var assembly = Assembly.GetEntryAssembly();

        if (assembly?.GetName().Name is not { } name ||
            !name.StartsWith(
                "DesktopAutomationBot.",
                StringComparison.Ordinal))
        {
            assembly = typeof(RuntimeNodeMetadataProvider).Assembly;
        }

        var informationalVersion =
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        return !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString()
                ?? "unknown";
    }

    private static string GetPlatformCapability()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return "unknown-os";
    }

    private static IReadOnlyDictionary<string, string>
        EmptyVersions() =>
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
}
