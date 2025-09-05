namespace MyServiceBus;

public static class MessageHeaders
{
    public const string FaultAddress = "MT-Fault-Address";

    public const string ExceptionType = "MT-ExceptionType";
    public const string ExceptionMessage = "MT-ExceptionMessage";
    public const string ExceptionStackTrace = "MT-ExceptionStackTrace";
    public const string Reason = "MT-Reason";
    public const string RedeliveryCount = "MT-RedeliveryCount";

    public const string HostMachineName = "MT-Host-MachineName";
    public const string HostProcessName = "MT-Host-ProcessName";
    public const string HostProcessId = "MT-Host-ProcessId";
    public const string HostAssembly = "MT-Host-Assembly";
    public const string HostAssemblyVersion = "MT-Host-AssemblyVersion";
    public const string HostFrameworkVersion = "MT-Host-FrameworkVersion";
    public const string HostMassTransitVersion = "MT-Host-MassTransitVersion";
    public const string HostOperatingSystemVersion = "MT-Host-OperatingSystemVersion";
}

