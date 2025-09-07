using System;
using System.Threading.Tasks;
using MyServiceBus;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;
using Xunit;

public class GenericRequestClientTests
{
    class OrderRequest
    {
        public bool Accept { get; set; }
    }

    class OrderAccepted
    {
        public string Message { get; set; } = string.Empty;
    }

    class OrderRejected
    {
        public string Reason { get; set; } = string.Empty;
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Sets_temporary_response_exchange_options()
    {
        var transportFactory = new MediatorTransportFactory();
        var serializer = new EnvelopeMessageSerializer();

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "order-response-options",
            ExchangeName = NamingConventions.GetExchangeName(typeof(OrderRequest))!,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var captured = new TaskCompletionSource<(Uri Response, Uri Fault)>(TaskCreationOptions.RunContinuationsAsynchronously);

        var receive = await transportFactory.CreateReceiveTransport(topology, [Throws(typeof(InvalidOperationException))] async (ctx) =>
        {
            captured.TrySetResult((ctx.ResponseAddress!, ctx.FaultAddress!));

            if (ctx.TryGetMessage<OrderRequest>(out var request))
            {
                var send = await transportFactory.GetSendTransport(ctx.ResponseAddress!);
                var types = MessageTypeCache.GetMessageTypes(typeof(OrderAccepted));
                var sendContext = new SendContext(types, serializer)
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                await send.Send(new OrderAccepted { Message = "ok" }, sendContext);
            }
        });

        await receive.Start();

        var client = new GenericRequestClient<OrderRequest>(transportFactory, serializer, new SendContextFactory());
        await client.GetResponseAsync<OrderAccepted>(new OrderRequest { Accept = true });

        var addresses = await captured.Task;
        Assert.Contains("durable=false", addresses.Response.Query);
        Assert.Contains("autodelete=true", addresses.Response.Query);
        Assert.Equal(addresses.Response, addresses.Fault);

        await receive.Stop();
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Returns_expected_response_for_multiple_types()
    {
        var transportFactory = new MediatorTransportFactory();
        var serializer = new EnvelopeMessageSerializer();

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "order-request",
            ExchangeName = NamingConventions.GetExchangeName(typeof(OrderRequest))!,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var receive = await transportFactory.CreateReceiveTransport(topology, [Throws(typeof(InvalidOperationException))] async (ctx) =>
        {
            if (ctx.TryGetMessage<OrderRequest>(out var request))
            {
                object response = request.Accept
                    ? new OrderAccepted { Message = "ok" }
                    : new OrderRejected { Reason = "no" };

                var send = await transportFactory.GetSendTransport(ctx.ResponseAddress!);
                var types = MessageTypeCache.GetMessageTypes(response.GetType());
                var sendContext = new SendContext(types, serializer)
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                await send.Send(response, sendContext);
            }
        });

        await receive.Start();

        var client = new GenericRequestClient<OrderRequest>(transportFactory, serializer, new SendContextFactory());

        var response = await client.GetResponseAsync<OrderAccepted, OrderRejected>(new OrderRequest { Accept = true });
        Assert.True(response.Is(out Response<OrderAccepted> accepted));
        Assert.False(response.Is(out Response<OrderRejected> rejected));

        response = await client.GetResponseAsync<OrderAccepted, OrderRejected>(new OrderRequest { Accept = false });
        Assert.False(response.Is(out accepted));
        Assert.True(response.Is(out rejected));

        await receive.Stop();
    }

    [Fact]
    [Throws(typeof(RequestFaultException))]
    public async Task Throws_when_fault_received()
    {
        var transportFactory = new MediatorTransportFactory();
        var serializer = new EnvelopeMessageSerializer();

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "order-fault",
            ExchangeName = NamingConventions.GetExchangeName(typeof(OrderRequest))!,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var receive = await transportFactory.CreateReceiveTransport(topology, [Throws(typeof(InvalidOperationException))] async (ctx) =>
        {
            if (ctx.TryGetMessage<OrderRequest>(out var request))
            {
                var send = await transportFactory.GetSendTransport(ctx.ResponseAddress!);
                var fault = new Fault<OrderRequest>
                {
                    Message = request,
                    Exceptions = [new ExceptionInfo { Message = "bad" }]
                };
                var types = MessageTypeCache.GetMessageTypes(fault.GetType());
                var sendContext = new SendContext(types, serializer)
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                await send.Send(fault, sendContext);
            }
        });

        await receive.Start();

        var client = new GenericRequestClient<OrderRequest>(transportFactory, serializer, new SendContextFactory());
        await Assert.ThrowsAsync<RequestFaultException>([Throws(typeof(UriFormatException), typeof(RequestFaultException))] () => client.GetResponseAsync<OrderAccepted, OrderRejected>(new OrderRequest { Accept = true }));

        await receive.Stop();
    }

    [Fact]
    [Throws(typeof(Exception))]
    public async Task Returns_fault_response_when_fault_type_expected()
    {
        var transportFactory = new MediatorTransportFactory();
        var serializer = new EnvelopeMessageSerializer();

        var topology = new ReceiveEndpointTopology
        {
            QueueName = "order-fault-response",
            ExchangeName = NamingConventions.GetExchangeName(typeof(OrderRequest))!,
            RoutingKey = "",
            ExchangeType = "fanout",
            Durable = true,
            AutoDelete = false
        };

        var receive = await transportFactory.CreateReceiveTransport(topology, [Throws(typeof(InvalidOperationException))] async (ctx) =>
        {
            if (ctx.TryGetMessage<OrderRequest>(out var request))
            {
                var send = await transportFactory.GetSendTransport(ctx.ResponseAddress!);
                var fault = new Fault<OrderRequest>
                {
                    Message = request,
                    Exceptions = [new ExceptionInfo { Message = "bad" }]
                };
                var types = MessageTypeCache.GetMessageTypes(fault.GetType());
                var sendContext = new SendContext(types, serializer)
                {
                    MessageId = Guid.NewGuid().ToString()
                };
                await send.Send(fault, sendContext);
            }
        });

        await receive.Start();

        var client = new GenericRequestClient<OrderRequest>(transportFactory, serializer, new SendContextFactory());
        var response = await client.GetResponseAsync<OrderAccepted, Fault<OrderRequest>>(new OrderRequest { Accept = true });

        Assert.True(response.Is(out Response<Fault<OrderRequest>> faultResponse));
        Assert.False(response.Is(out Response<OrderAccepted> accepted));

        await receive.Stop();
    }

}
