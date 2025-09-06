package com.myservicebus;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

public class ExceptionInfoTest {
    @Test
    public void fromException_capturesDetails() {
        IllegalArgumentException inner = new IllegalArgumentException("inner");
        IllegalStateException ex = new IllegalStateException("outer", inner);

        ExceptionInfo info = ExceptionInfo.fromException(ex);

        assertEquals(IllegalStateException.class.getName(), info.getExceptionType());
        assertEquals("outer", info.getMessage());
        assertNotNull(info.getStackTrace());
        assertEquals(IllegalArgumentException.class.getName(), info.getInnerException().getExceptionType());
    }
}
