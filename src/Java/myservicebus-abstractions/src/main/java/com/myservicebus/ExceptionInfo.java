package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.Data;

@Data
public class ExceptionInfo {
    @JsonProperty("exceptionType")
    private String exceptionType;

    @JsonProperty("message")
    private String message;

    @JsonProperty("stackTrace")
    private String stackTrace;

    @JsonProperty("source")
    private String source;

    @JsonProperty("innerException")
    private ExceptionInfo innerException;

    public static ExceptionInfo fromException(Throwable exception) {
        ExceptionInfo info = new ExceptionInfo();
        info.exceptionType = exception.getClass().getName();
        info.message = exception.getMessage();
        info.stackTrace = getStackTrace(exception);
        Package pkg = exception.getClass().getPackage();
        info.source = pkg != null ? pkg.getName() : null;
        Throwable cause = exception.getCause();
        if (cause != null) {
            info.innerException = fromException(cause);
        }
        return info;
    }

    private static String getStackTrace(Throwable ex) {
        java.io.StringWriter sw = new java.io.StringWriter();
        java.io.PrintWriter pw = new java.io.PrintWriter(sw);
        ex.printStackTrace(pw);
        return sw.toString();
    }
}
