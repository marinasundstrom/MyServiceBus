package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.UUID;

import org.junit.jupiter.api.Test;

class JobContractTest {
    @Test
    void submissionOptionsRejectEmptyIdentifier() {
        assertThrows(IllegalArgumentException.class,
                () -> new JobSubmissionOptions(new UUID(0, 0)));
    }

    @Test
    void progressValidatesItsRange() {
        assertEquals(new JobProgress(2, 10L), new JobProgress(2, 10L));
        assertThrows(IllegalArgumentException.class, () -> new JobProgress(-1));
        assertThrows(IllegalArgumentException.class, () -> new JobProgress(1, 0L));
        assertThrows(IllegalArgumentException.class, () -> new JobProgress(11, 10L));
    }
}

