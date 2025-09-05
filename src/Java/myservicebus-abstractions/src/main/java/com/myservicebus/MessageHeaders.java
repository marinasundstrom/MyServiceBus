package com.myservicebus;

public final class MessageHeaders {
    private MessageHeaders() {}

    public static final String FAULT_ADDRESS = "MT-Fault-Address";

    public static final String EXCEPTION_TYPE = "MT-ExceptionType";
    public static final String EXCEPTION_MESSAGE = "MT-ExceptionMessage";
    public static final String EXCEPTION_STACKTRACE = "MT-ExceptionStackTrace";
    public static final String REASON = "MT-Reason";
    public static final String REDELIVERY_COUNT = "MT-RedeliveryCount";

    public static final String HOST_MACHINE = "MT-Host-MachineName";
    public static final String HOST_PROCESS = "MT-Host-ProcessName";
    public static final String HOST_PROCESS_ID = "MT-Host-ProcessId";
    public static final String HOST_ASSEMBLY = "MT-Host-Assembly";
    public static final String HOST_ASSEMBLY_VERSION = "MT-Host-AssemblyVersion";
    public static final String HOST_FRAMEWORK_VERSION = "MT-Host-FrameworkVersion";
    public static final String HOST_MASS_TRANSIT_VERSION = "MT-Host-MassTransitVersion";
    public static final String HOST_OS_VERSION = "MT-Host-OperatingSystemVersion";
}
