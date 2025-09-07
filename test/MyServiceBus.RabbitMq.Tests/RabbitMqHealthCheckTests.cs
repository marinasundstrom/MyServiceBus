using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using RabbitMQ.Client;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyServiceBus.RabbitMq.Tests;

public class RabbitMqHealthCheckTests
{
    [Fact]
    public async Task ReportsHealthyWhenConnectionOpen()
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        var factory = Substitute.For<IConnectionFactory>();
        factory.CreateConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(connection));

        var provider = new ConnectionProvider(factory);
        var check = new RabbitMqHealthCheck(provider);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReportsUnhealthyWhenConnectionClosed()
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(false);
        var factory = Substitute.For<IConnectionFactory>();
        factory.CreateConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(connection));

        var provider = new ConnectionProvider(factory);
        var check = new RabbitMqHealthCheck(provider);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}

