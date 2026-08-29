package com.myservicebus;

import com.myservicebus.serialization.MessageIntent;
import com.myservicebus.serialization.MessageSerializationContext;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageBody;
import java.net.URI;
import java.time.OffsetDateTime;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.myservicebus.tasks.CancellationToken;

import java.time.Instant;

public class SendContext implements PipeContext, ScheduledMessage {
    private Object message;
    private final Map<String, Object> headers = new HashMap<>();
    private final CancellationToken cancellationToken;
    private URI sourceAddress;
    private URI destinationAddress;
    private URI responseAddress;
    private URI faultAddress;
    private Instant scheduledEnqueueTime;
    private UUID messageId = UUID.randomUUID();
    private UUID requestId;
    private UUID correlationId;
    private UUID conversationId = UUID.randomUUID();
    private UUID initiatorId;
    private MessageIntent intent = MessageIntent.SEND;
    private List<String> messageTypes;

    public SendContext(Object message) {
        this(message, CancellationToken.none());
    }

    public SendContext(Object message, CancellationToken cancellationToken) {
        this.message = message;
        this.cancellationToken = cancellationToken;
    }

    public Object getMessage() {
        return message;
    }

    public void setMessage(Object message) {
        this.message = message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    public URI getSourceAddress() {
        return sourceAddress;
    }

    public void setSourceAddress(URI sourceAddress) {
        this.sourceAddress = sourceAddress;
    }

    public URI getDestinationAddress() {
        return destinationAddress;
    }

    public void setDestinationAddress(URI destinationAddress) {
        this.destinationAddress = destinationAddress;
    }

    public URI getResponseAddress() {
        return responseAddress;
    }

    public void setResponseAddress(URI responseAddress) {
        this.responseAddress = responseAddress;
    }

    public URI getFaultAddress() {
        return faultAddress;
    }

    public void setFaultAddress(URI faultAddress) {
        this.faultAddress = faultAddress;
    }

    public UUID getMessageId() {
        return messageId;
    }

    public void setMessageId(UUID messageId) {
        this.messageId = messageId;
    }

    public UUID getRequestId() {
        return requestId;
    }

    public void setRequestId(UUID requestId) {
        this.requestId = requestId;
    }

    public UUID getCorrelationId() {
        return correlationId;
    }

    public void setCorrelationId(UUID correlationId) {
        this.correlationId = correlationId;
    }

    public UUID getConversationId() {
        return conversationId;
    }

    public void setConversationId(UUID conversationId) {
        this.conversationId = conversationId;
    }

    public UUID getInitiatorId() {
        return initiatorId;
    }

    public void setInitiatorId(UUID initiatorId) {
        this.initiatorId = initiatorId;
    }

    public MessageIntent getIntent() {
        return intent;
    }

    public void setIntent(MessageIntent intent) {
        this.intent = intent;
    }

    public void setMessageTypes(List<String> messageTypes) {
        this.messageTypes = messageTypes == null ? null : List.copyOf(messageTypes);
    }

    public List<String> getMessageTypes() {
        return messageTypes == null ? null : List.copyOf(messageTypes);
    }

    @Override
    public Instant getScheduledEnqueueTime() {
        return scheduledEnqueueTime;
    }

    @Override
    public void setScheduledEnqueueTime(Instant scheduledTime) {
        this.scheduledEnqueueTime = scheduledTime;
    }

    public MessageBody getMessageBody(MessageSerializer serializer) throws Exception {
        MessageSerializationContext<Object> context = new MessageSerializationContext<>(message);
        context.setMessageId(messageId);
        context.setRequestId(requestId);
        context.setCorrelationId(correlationId);
        context.setConversationId(conversationId);
        context.setInitiatorId(initiatorId);
        context.setIntent(intent);
        context.setMessageType(messageTypes != null ? messageTypes : MessageUrn.forMessageTypes(message.getClass()));
        context.setResponseAddress(responseAddress);
        context.setFaultAddress(faultAddress);
        context.setSourceAddress(sourceAddress != null ? sourceAddress : URI.create("loopback://localhost/source"));
        context.setDestinationAddress(
                destinationAddress != null ? destinationAddress
                        : URI.create("loopback://localhost/" + message.getClass().getSimpleName()));
        context.setHeaders(headers);
        context.setSentTime(OffsetDateTime.now());
        context.setHostInfo(HostInfoProvider.capture());
        return serializer.getMessageBody(context);
    }

    public byte[] serialize(MessageSerializer serializer) throws Exception {
        return getMessageBody(serializer).getBytes();
    }

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}
