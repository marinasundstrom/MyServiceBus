using System.Diagnostics;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using TestApp;

namespace MyServiceBus.RabbitMq.Tests;

public class CrossLanguageRabbitMqTests
{
    [CrossLanguageFact]
    public async Task Csharp_producer_delivers_to_java_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"csharp-to-java-{suffix}";
        var queueName = exchangeName;
        const string expectedValue = "from-csharp";
        using var javaPeer = StartJavaPeer(container, "consume", exchangeName, queueName, expectedValue);

        await WaitForOutput(javaPeer, "READY", TimeSpan.FromSeconds(60));

        var transportFactory = CreateTransportFactory(container);
        var serializer = new EnvelopeMessageSerializer();
        var sendContext = new RabbitMqSendContext([typeof(CrossLanguageMessage)], serializer);
        var sendTransport = await transportFactory.GetSendTransport(
            new Uri($"exchange:{exchangeName}"));
        await sendTransport.Send(new CrossLanguageMessage { Value = expectedValue }, sendContext);

        await WaitForOutput(javaPeer, "RECEIVED", TimeSpan.FromSeconds(20));
        await WaitForExit(javaPeer, TimeSpan.FromSeconds(10));
        Assert.Equal(0, javaPeer.ExitCode);
    }

    [CrossLanguageFact]
    public async Task Java_producer_delivers_to_csharp_consumer()
    {
        await using var container = new RabbitMqBuilder("rabbitmq:4.1-alpine").Build();
        await container.StartAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var exchangeName = $"java-to-csharp-{suffix}";
        var queueName = exchangeName;
        const string expectedValue = "from-java";
        var transportFactory = CreateTransportFactory(container);
        var received = new TaskCompletionSource<CrossLanguageMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveTransport = await transportFactory.CreateReceiveTransport(
            new ReceiveEndpointTopology
            {
                QueueName = queueName,
                ExchangeName = exchangeName,
                Durable = false,
                AutoDelete = true
            },
            context =>
            {
                if (context.TryGetMessage<CrossLanguageMessage>(out var message))
                    received.TrySetResult(message);

                return Task.CompletedTask;
            },
            messageType => messageType == MessageUrn.For(typeof(CrossLanguageMessage)));

        await receiveTransport.Start();
        try
        {
            using var javaPeer = StartJavaPeer(container, "produce", exchangeName, queueName, expectedValue);
            await WaitForOutput(javaPeer, "SENT", TimeSpan.FromSeconds(60));
            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await WaitForExit(javaPeer, TimeSpan.FromSeconds(10));

            Assert.Equal(0, javaPeer.ExitCode);
            Assert.Equal(expectedValue, message.Value);
        }
        finally
        {
            await receiveTransport.Stop();
        }
    }

    private static RabbitMqTransportFactory CreateTransportFactory(RabbitMqContainer container)
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(container.GetConnectionString())
        };
        return new RabbitMqTransportFactory(
            new ConnectionProvider(connectionFactory),
            new RabbitMqFactoryConfigurator());
    }

    private static Process StartJavaPeer(
        RabbitMqContainer container,
        string mode,
        string exchangeName,
        string queueName,
        string value)
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
        startInfo.ArgumentList.Add($"--args={mode} {exchangeName} {queueName} {value}");
        startInfo.Environment["RABBITMQ_HOST"] = connectionUri.Host;
        startInfo.Environment["RABBITMQ_PORT"] = connectionUri.Port.ToString();
        var credentials = connectionUri.UserInfo.Split(':', 2);
        startInfo.Environment["RABBITMQ_USERNAME"] = Uri.UnescapeDataString(credentials[0]);
        startInfo.Environment["RABBITMQ_PASSWORD"] = Uri.UnescapeDataString(credentials[1]);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Java interoperability peer.");
    }

    private static async Task WaitForOutput(Process process, string expectedLine, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (await process.StandardOutput.ReadLineAsync(cancellation.Token) is { } line)
        {
            if (line == expectedLine)
                return;
        }

        var error = await process.StandardError.ReadToEndAsync(cancellation.Token);
        throw new InvalidOperationException(
            $"Java interoperability peer exited before writing '{expectedLine}'. {error}");
    }

    private static async Task WaitForExit(Process process, TimeSpan timeout)
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
