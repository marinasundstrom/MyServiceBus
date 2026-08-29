package com.myservicebus.amazon.sqs;

import com.myservicebus.MessageHeaders;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import software.amazon.awssdk.services.sns.model.PublishRequest;
import software.amazon.awssdk.services.sqs.model.Message;
import software.amazon.awssdk.services.sqs.model.SendMessageRequest;

import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

final class AmazonSqsMessageMapper {
    private static final String CONTENT_TYPE = "Content-Type";

    private AmazonSqsMessageMapper() { }

    static SendMessageRequest sqsRequest(String queueUrl, byte[] body, String contentType) {
        return SendMessageRequest.builder().queueUrl(queueUrl)
                .messageBody(new String(body, StandardCharsets.UTF_8))
                .messageAttributes(Map.of(CONTENT_TYPE,
                        software.amazon.awssdk.services.sqs.model.MessageAttributeValue.builder()
                                .dataType("String").stringValue(contentType).build()))
                .build();
    }

    static PublishRequest snsRequest(String topicArn, byte[] body, String contentType) {
        return PublishRequest.builder().topicArn(topicArn)
                .message(new String(body, StandardCharsets.UTF_8))
                .messageAttributes(Map.of(CONTENT_TYPE,
                        software.amazon.awssdk.services.sns.model.MessageAttributeValue.builder()
                                .dataType("String").stringValue(contentType).build()))
                .build();
    }

    static Map<String, Object> headers(Message message, String faultAddress) {
        Map<String, Object> headers = new HashMap<>();
        var contentType = message.messageAttributes().get(CONTENT_TYPE);
        headers.put(MassTransitHeaderConvention.INSTANCE.getContentTypeHeader(),
                contentType != null ? contentType.stringValue() : "application/vnd.masstransit+json");
        if (message.messageId() != null) headers.put("message_id", message.messageId());
        String receiveCount = message.attributesAsStrings().get("ApproximateReceiveCount");
        if (receiveCount != null) headers.put(MessageHeaders.REDELIVERY_COUNT,
                Math.max(0, Integer.parseInt(receiveCount) - 1));
        if (faultAddress != null) headers.putIfAbsent(
                MassTransitHeaderConvention.INSTANCE.getFaultAddressHeader(), faultAddress);
        return headers;
    }
}
