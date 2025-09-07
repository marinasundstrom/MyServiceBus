package com.myservicebus.logging;

import static org.junit.jupiter.api.Assertions.*;

import java.io.ByteArrayOutputStream;
import java.io.PrintStream;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

public class ConsoleLoggerTest {
    private PrintStream originalOut;
    private PrintStream originalErr;
    private ByteArrayOutputStream outContent;
    private ByteArrayOutputStream errContent;

    @BeforeEach
    void setUp() {
        originalOut = System.out;
        originalErr = System.err;
        outContent = new ByteArrayOutputStream();
        errContent = new ByteArrayOutputStream();
        System.setOut(new PrintStream(outContent));
        System.setErr(new PrintStream(errContent));
    }

    @AfterEach
    void tearDown() {
        System.setOut(originalOut);
        System.setErr(originalErr);
    }

    @Test
    void respectsMinimumLevel() {
        ConsoleLoggerConfig config = new ConsoleLoggerConfig().setMinimumLevel(LogLevel.INFO);
        Logger logger = new ConsoleLoggerFactory(config).create("test");

        logger.debug("debug message");
        logger.info("info message");

        assertFalse(outContent.toString().contains("debug message"));
        assertTrue(outContent.toString().contains("info message"));
    }

    @Test
    void categoryOverridesMinimumLevel() {
        ConsoleLoggerConfig config = new ConsoleLoggerConfig().setMinimumLevel(LogLevel.ERROR);
        config.setLevel("test", LogLevel.DEBUG);
        Logger logger = new ConsoleLoggerFactory(config).create("test");

        logger.debug("debug message");

        assertTrue(outContent.toString().contains("debug message"));
    }

    @Test
    void writesErrorsToErrStream() {
        ConsoleLoggerConfig config = new ConsoleLoggerConfig().setMinimumLevel(LogLevel.DEBUG);
        Logger logger = new ConsoleLoggerFactory(config).create("test");

        logger.error("error message");

        assertTrue(errContent.toString().contains("error message"));
    }
}
