namespace MyServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MyServiceBus.Serialization;
using Xunit;

public class ReceiveContextFaultAddressTests
{
    [Fact]
    [Throws(typeof(EncoderFallbackException), typeof(UriFormatException), typeof(JsonException))]
    public void Envelope_context_reads_fault_address_from_transport_headers()
    {
        var json = Encoding.UTF8.GetBytes("{\"messageId\":\"00000000-0000-0000-0000-000000000000\",\"messageType\":[],\"message\":{}}");
        var headers = new Dictionary<string, object>
        {
            [MessageHeaders.FaultAddress] = "rabbitmq://localhost/exchange/test_queue_error"
        };
        var envelope = new EnvelopeMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(envelope, null);
        Assert.Equal(new Uri("rabbitmq://localhost/exchange/test_queue_error"), receiveContext.FaultAddress);
    }

    [Fact]
    [Throws(typeof(EncoderFallbackException), typeof(UriFormatException), typeof(JsonException))]
    public void Raw_context_reads_fault_address_from_transport_headers()
    {
        var json = Encoding.UTF8.GetBytes("{}");
        var headers = new Dictionary<string, object>
        {
            [MessageHeaders.FaultAddress] = "rabbitmq://localhost/exchange/test_queue_error"
        };
        var raw = new RawJsonMessageContext(json, headers);
        var receiveContext = new ReceiveContextImpl(raw, null);
        Assert.Equal(new Uri("rabbitmq://localhost/exchange/test_queue_error"), receiveContext.FaultAddress);
    }
}
