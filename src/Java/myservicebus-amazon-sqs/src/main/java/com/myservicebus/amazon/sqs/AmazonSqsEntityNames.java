package com.myservicebus.amazon.sqs;

import com.myservicebus.EntityName;
import com.myservicebus.MessageEntityNameFormatter;

import java.util.Objects;
import java.util.regex.Pattern;

final class AmazonSqsEntityNames {
    private static final Pattern VALID = Pattern.compile("^[A-Za-z0-9_-]{1,80}$");
    private static final Pattern VALID_TOPIC = Pattern.compile("^[A-Za-z0-9_-]{1,256}$");

    private AmazonSqsEntityNames() {
    }

    static String format(String value) {
        String normalized = value.replaceAll("[^A-Za-z0-9_-]+", "-").replaceAll("^-+|-+$", "");
        if (normalized.length() > 80) {
            normalized = normalized.substring(0, 80);
        }
        validate(normalized);
        return normalized;
    }

    static void validate(String value) {
        if (value == null || !VALID.matcher(value).matches()) {
            throw new IllegalArgumentException(
                    "Amazon SQS queue names must contain 1-80 letters, digits, hyphens, or underscores");
        }
    }

    static String formatTopic(String value) {
        String normalized = value.replaceAll("[^A-Za-z0-9_-]+", "-");
        validateTopic(normalized);
        return normalized;
    }

    static void validateTopic(String value) {
        if (value == null || !VALID_TOPIC.matcher(value).matches()) {
            throw new IllegalArgumentException(
                    "Amazon SNS topic names must contain 1-256 letters, digits, hyphens, or underscores");
        }
    }

    static String companion(String value, String suffix) {
        validate(value);
        Objects.requireNonNull(suffix);
        int length = 80 - suffix.length();
        if (length < 1) {
            throw new IllegalArgumentException("Amazon SQS companion suffix is too long");
        }
        return (value.length() > length ? value.substring(0, length) : value) + suffix;
    }

    static final class Formatter implements MessageEntityNameFormatter {
        static final Formatter INSTANCE = new Formatter();

        @Override
        public String formatEntityName(Class<?> messageType) {
            EntityName configured = messageType.getAnnotation(EntityName.class);
            if (configured != null) {
                return formatTopic(configured.value());
            }

            StringBuilder result = new StringBuilder();
            Package messagePackage = messageType.getPackage();
            if (messagePackage != null && !messagePackage.getName().isBlank()) {
                result.append(messagePackage.getName().replace('.', '_')).append('-');
            }
            appendClassName(result, messageType);
            return formatTopic(result.toString());
        }

        private static void appendClassName(StringBuilder result, Class<?> messageType) {
            if (messageType.getEnclosingClass() != null) {
                appendClassName(result, messageType.getEnclosingClass());
                result.append('_');
            }
            result.append(messageType.getSimpleName());
        }
    }
}
