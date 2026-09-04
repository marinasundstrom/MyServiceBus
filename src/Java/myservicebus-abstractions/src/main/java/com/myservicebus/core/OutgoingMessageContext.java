package com.myservicebus.core;

import java.net.URI;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.myservicebus.serialization.MessageIntent;
import com.myservicebus.PipeContext;
import com.myservicebus.ScheduledMessage;

/**
 * Mutable outgoing-message state shared by runtime implementations and
 * language-specific send and publish context projections.
 */
public interface OutgoingMessageContext extends PipeContext, ScheduledMessage {
    Object getMessage();

    void setMessage(Object message);

    Map<String, Object> getHeaders();

    URI getSourceAddress();

    void setSourceAddress(URI sourceAddress);

    URI getDestinationAddress();

    void setDestinationAddress(URI destinationAddress);

    URI getResponseAddress();

    void setResponseAddress(URI responseAddress);

    URI getFaultAddress();

    void setFaultAddress(URI faultAddress);

    UUID getMessageId();

    void setMessageId(UUID messageId);

    UUID getRequestId();

    void setRequestId(UUID requestId);

    UUID getCorrelationId();

    void setCorrelationId(UUID correlationId);

    UUID getConversationId();

    void setConversationId(UUID conversationId);

    UUID getInitiatorId();

    void setInitiatorId(UUID initiatorId);

    UUID getCausationMessageId();

    void setCausationMessageId(UUID causationMessageId);

    MessageIntent getIntent();

    void setIntent(MessageIntent intent);

    List<String> getMessageTypes();

    void setMessageTypes(List<String> messageTypes);

    @Override
    Instant getScheduledEnqueueTime();

    @Override
    void setScheduledEnqueueTime(Instant scheduledTime);
}
