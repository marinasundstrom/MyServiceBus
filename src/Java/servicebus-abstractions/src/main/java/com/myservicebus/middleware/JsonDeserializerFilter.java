package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import javax.sound.sampled.AudioFormat.Encoding;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import com.myservicebus.MessageDispatcher;
import com.myservicebus.MessageTypeResolver;
import com.myservicebus.contexts.ReceiveContext;

public class JsonDeserializerFilter implements Filter<ReceiveContext> {
    private final MessageTypeResolver _resolver;

    public JsonDeserializerFilter(MessageTypeResolver resolver) {
        _resolver = resolver;
    }

    public CompletableFuture<Void> send(ReceiveContext context, Pipe<ReceiveContext> next)
            throws Exception {

        var messageType = _resolver.resolve(context.getMessageType());

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules(); // For Java time serialization
        mapper.enable(SerializationFeature.INDENT_OUTPUT); // Pretty print
        mapper.disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mapper.registerModule(new JavaTimeModule());

        var body = context.getBody();
        var message = mapper.writeValueAsString(body);

        context.getServices().getService(MessageDispatcher.class)
                .dispatch(message, context); // forwards to ConsumePipe<T>

        return next.send(context);
    }

    // public void Probe(ProbeContext context) {
    // }
}