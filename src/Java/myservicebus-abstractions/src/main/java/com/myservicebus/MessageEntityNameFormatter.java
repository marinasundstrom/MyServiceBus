package com.myservicebus;

public interface MessageEntityNameFormatter {
    String formatEntityName(Class<?> messageType);
}
