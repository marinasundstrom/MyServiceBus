package com.myservicebus.amazon.sqs;

import com.myservicebus.SendTransport;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sqs.SqsClient;

import java.util.Map;

public final class AmazonSqsSendTransport implements SendTransport {
    private final SqsClient sqs;
    private final SnsClient sns;
    private final AmazonSqsEndpointAddress.EntityKind kind;
    private final String destination;
    private final String entityName;

    AmazonSqsSendTransport(SqsClient sqs, SnsClient sns, AmazonSqsEndpointAddress.EntityKind kind,
            String destination, String entityName) {
        this.sqs = sqs;
        this.sns = sns;
        this.kind = kind;
        this.destination = destination;
        this.entityName = entityName;
    }

    @Override
    public void send(byte[] data, Map<String, Object> headers, String contentType) {
        try {
            if (data.length > 1_048_576) {
                throw new IllegalArgumentException("Amazon SQS/SNS messages cannot exceed 1 MiB");
            }
            if (kind == AmazonSqsEndpointAddress.EntityKind.TOPIC) {
                sns.publish(AmazonSqsMessageMapper.snsRequest(destination, data, contentType));
            } else {
                sqs.sendMessage(AmazonSqsMessageMapper.sqsRequest(destination, data, contentType));
            }
        } catch (Exception exception) {
            throw exception instanceof AmazonSqsTransportException transportException
                    ? transportException : new AmazonSqsTransportException("send", entityName, exception);
        }
    }
}
