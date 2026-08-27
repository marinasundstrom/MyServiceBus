package com.myservicebus;

import java.util.concurrent.CompletionException;

public final class ErrorTransportSettlement {
    private ErrorTransportSettlement() {
    }

    public static void markMoved(Throwable exception, String errorAddress) {
        if (exception == null) {
            throw new IllegalArgumentException("Exception cannot be null");
        }
        if (errorAddress == null || errorAddress.isBlank()) {
            throw new IllegalArgumentException("Error address cannot be blank");
        }
        exception.addSuppressed(new MovedMarker(errorAddress));
    }

    public static boolean wasMoved(Throwable exception) {
        Throwable current = exception;
        while (current != null) {
            for (Throwable suppressed : current.getSuppressed()) {
                if (suppressed instanceof MovedMarker) {
                    return true;
                }
            }
            current = current instanceof CompletionException ? current.getCause() : null;
        }
        return false;
    }

    public static String getErrorAddress(Throwable exception) {
        Throwable current = exception;
        while (current != null) {
            for (Throwable suppressed : current.getSuppressed()) {
                if (suppressed instanceof MovedMarker marker) {
                    return marker.errorAddress;
                }
            }
            current = current instanceof CompletionException ? current.getCause() : null;
        }
        return null;
    }

    private static final class MovedMarker extends RuntimeException {
        private final String errorAddress;

        private MovedMarker(String errorAddress) {
            super("The failed message was moved to error destination '" + errorAddress + "'.");
            this.errorAddress = errorAddress;
        }
    }
}
