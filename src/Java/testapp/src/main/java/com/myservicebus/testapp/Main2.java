package com.myservicebus.testapp;

import java.net.InetAddress;
import java.net.UnknownHostException;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.Envelope;
import com.myservicebus.Fault;
import com.myservicebus.HostInfo;

public class Main2 {

    private static void test() throws JsonProcessingException, UnknownHostException {
        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules(); // For Java time serialization
        mapper.enable(SerializationFeature.INDENT_OUTPUT); // Pretty print
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mapper.registerModule(new JavaTimeModule());

        testMessageDeserializationWithFaultWrapper(mapper);

        // testMessageDeserialization(mapper);
    }

    private static void testMessageDeserializationWithFaultWrapper(ObjectMapper mapper)
            throws JsonProcessingException, UnknownHostException {
        // 📨 Step 1: Build original message
        SubmitOrder message = new SubmitOrder(UUID.randomUUID());

        // 🚨 Step 2: Wrap in Fault<T>
        Fault<SubmitOrder> fault = new Fault<>();
        fault.setMessage(message);
        fault.setFaultId(UUID.randomUUID());
        fault.setMessageId(UUID.randomUUID());
        fault.setSentTime(OffsetDateTime.now());
        fault.setExceptions(List.of()); // empty for demo

        // 🧠 Step 3: Wrap Fault<T> in Envelope<Fault<T>>
        Envelope<Fault<SubmitOrder>> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setMessage(fault);
        envelope.setMessageType(List.of(
                "urn:message:MassTransit:Fault`1[[com.myservicebus.model.MyMessage, com.myservicebus, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"));
        envelope.setContentType("application/json");
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setSourceAddress("rabbitmq://localhost/source");
        envelope.setDestinationAddress("rabbitmq://localhost/destination");

        String machineName = InetAddress.getLocalHost().getHostName();
        String processName = "java";
        long processId = ProcessHandle.current().pid(); // Java 9+
        String serviceName = "my-app";
        String serviceVersion = "1.0.0";
        String frameworkVersion = System.getProperty("java.version");
        String platform = "8.0.10.0"; // define or detect if needed
        String operatingSystemVersion = System.getProperty("os.name") + " " + System.getProperty("os.version");

        HostInfo host = new HostInfo(
                machineName,
                processName,
                (int) processId,
                serviceName,
                serviceVersion,
                frameworkVersion,
                platform,
                operatingSystemVersion);
        envelope.setHost(host);

        // 📝 Step 4: Serialize
        String json = mapper.writeValueAsString(envelope);
        System.out.println("📦 Serialized Fault Envelope:\n" + json);

        // 🔄 Step 5: Deserialize using type reference
        JavaType faultType = mapper.getTypeFactory().constructParametricType(Fault.class, SubmitOrder.class);
        JavaType envelopeType = mapper.getTypeFactory().constructParametricType(Envelope.class, faultType);

        Envelope<Fault<SubmitOrder>> deserialized = mapper.readValue(json, envelopeType);

        // ✅ Step 6: Access the original message
        SubmitOrder innerMessage = deserialized.getMessage().getMessage();
        System.out.println("\n✅ Deserialized inner message:");
        System.out.println("Order ID: " + innerMessage.getOrderId());
    }

    private static void testWrap(ObjectMapper mapper) throws JsonProcessingException {
        // Create a message
        SubmitOrder message = new SubmitOrder(UUID.randomUUID());

        // Create envelope
        Envelope<SubmitOrder> envelope = new Envelope<>();
        envelope.setMessageId(UUID.randomUUID());
        envelope.setSentTime(OffsetDateTime.now());
        envelope.setSourceAddress("rabbitmq://localhost/source");
        envelope.setDestinationAddress("rabbitmq://localhost/destination");
        envelope.setMessageType(List.of("urn:message:com.myservicebus:MyMessage"));
        envelope.setMessage(message);
        envelope.setHeaders(Map.of("Application-Header", "SomeValue"));
        envelope.setContentType("application/json");

        // Host info (mocked)
        HostInfo host = new HostInfo(
                "MyMachine",
                "java",
                12345,
                "my-service",
                "1.0.0",
                System.getProperty("java.version"),
                "8.0.10.0",
                System.getProperty("os.name") + " " + System.getProperty("os.version"));
        envelope.setHost(host);

        // Serialize to JSON
        String json = mapper.writeValueAsString(envelope);

        // Print to console
        System.out.println("📦 Serialized Envelope:");
        System.out.println(json);
    }
}