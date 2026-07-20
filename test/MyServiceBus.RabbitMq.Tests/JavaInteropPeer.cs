using System.Diagnostics;
using Testcontainers.RabbitMq;

namespace MyServiceBus.RabbitMq.Tests;

internal static class JavaInteropPeer
{
    public static Process Start(
        RabbitMqContainer container,
        string mode,
        string exchangeName,
        string queueName,
        string value,
        bool durableExchange = false)
    {
        var connectionUri = new Uri(container.GetConnectionString());
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
        startInfo.ArgumentList.Add(
            $"--args={mode} {exchangeName} {queueName} {value} {durableExchange.ToString().ToLowerInvariant()}");
        startInfo.Environment["RABBITMQ_HOST"] = connectionUri.Host;
        startInfo.Environment["RABBITMQ_PORT"] = connectionUri.Port.ToString();
        var credentials = connectionUri.UserInfo.Split(':', 2);
        startInfo.Environment["RABBITMQ_USERNAME"] = Uri.UnescapeDataString(credentials[0]);
        startInfo.Environment["RABBITMQ_PASSWORD"] = Uri.UnescapeDataString(credentials[1]);

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
