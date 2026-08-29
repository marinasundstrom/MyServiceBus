package com.myservicebus.amazon.sqs;

import com.myservicebus.EntityNameFormatter;
import com.myservicebus.MessageEntityNameFormatter;

import java.util.Objects;
import java.util.regex.Pattern;

final class AmazonSqsEntityNames {
    private static final Pattern VALID = Pattern.compile("^[A-Za-z0-9_-]{1,80}$");

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
                    "Amazon SQS/SNS entity names must contain 1-80 letters, digits, hyphens, or underscores");
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
            return format(EntityNameFormatter.format(messageType));
        }
    }
}
