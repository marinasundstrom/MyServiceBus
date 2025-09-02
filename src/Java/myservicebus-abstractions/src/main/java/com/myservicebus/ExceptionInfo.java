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
        info.setExceptionType(exception.getClass().getName());
        info.setMessage(exception.getMessage());
        info.setStackTrace(getStackTrace(exception));
        info.setSource(exception.getClass().getPackageName());
        if (exception.getCause() != null) {
            info.setInnerException(fromException(exception.getCause()));
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
