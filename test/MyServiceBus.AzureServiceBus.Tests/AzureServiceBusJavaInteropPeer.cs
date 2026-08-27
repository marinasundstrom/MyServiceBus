using System.Diagnostics;

namespace MyServiceBus.AzureServiceBus.Tests;

internal static class AzureServiceBusJavaInteropPeer
{
    public static Process Start(
        string connectionString,
        string mode,
        string queueOrEntity,
        string bindingOrUnused,
        string value,
        bool createTopology = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("GRADLE_COMMAND") ?? "gradle",
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--console=plain");
        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add(":interop-test-peer:run");
        startInfo.ArgumentList.Add($"--args={mode} {queueOrEntity} {bindingOrUnused} {value}");
        startInfo.Environment["AZURE_SERVICEBUS_CONNECTION_STRING"] = connectionString;
        if (createTopology)
            startInfo.Environment["AZURE_SERVICEBUS_CREATE_TOPOLOGY"] = "1";

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Java interoperability peer.");
    }

    public static async Task WaitForOutput(Process process, string expectedLine, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellation.Token) is { } line)
            {
                if (line == expectedLine)
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw new TimeoutException(
                $"Java interoperability peer did not write '{expectedLine}' within {timeout}.");
        }

        var error = await process.StandardError.ReadToEndAsync();
        throw new InvalidOperationException(
            $"Java interoperability peer exited before writing '{expectedLine}'. {error}");
    }

    public static async Task WaitForExit(Process process, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        await process.WaitForExitAsync(cancellation.Token);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "settings.gradle")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
