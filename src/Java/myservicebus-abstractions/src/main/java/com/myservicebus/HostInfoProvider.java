package com.myservicebus;

public final class HostInfoProvider {
    private HostInfoProvider() {
    }

    public static HostInfo capture() {
        String machine;
        try {
            machine = java.net.InetAddress.getLocalHost().getHostName();
        } catch (Exception ex) {
            machine = "unknown";
        }

        String processName = java.lang.management.ManagementFactory.getRuntimeMXBean().getName();
        int pid = (int) ProcessHandle.current().pid();
        String command = System.getProperty("sun.java.command", "unknown");

        String assemblyVersion = HostInfoProvider.class.getPackage().getImplementationVersion();
        if (assemblyVersion == null) {
            assemblyVersion = "unknown";
        }

        String framework = System.getProperty("java.version");

        String massTransitVersion = HostInfo.class.getPackage().getImplementationVersion();
        if (massTransitVersion == null) {
            massTransitVersion = assemblyVersion;
        }

        String os = System.getProperty("os.name") + " " + System.getProperty("os.version");

        return new HostInfo(machine, processName, pid, command, assemblyVersion, framework, massTransitVersion, os);
    }
}
