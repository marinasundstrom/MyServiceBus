package com.myservicebus.tasks;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CancellationException;

import org.junit.jupiter.api.Test;

class CancellationTokenTest {
    @Test
    void noneReturnsTheSharedNonCancelledToken() {
        assertSame(CancellationToken.none(), CancellationToken.none());
        assertFalse(CancellationToken.none().isCancelled());
        CancellationToken.none().throwIfCancelled();
    }

    @Test
    void sourceExposesAReadOnlyTokenThatThrowsAfterCancellation() {
        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.token();

        source.cancel();

        assertTrue(token.isCancelled());
        assertThrows(CancellationException.class, token::throwIfCancelled);
    }
}
