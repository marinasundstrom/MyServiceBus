namespace TestApp;

public static class DemoScenario
{
    public static string CreateSubmitMessage(string origin, bool shouldFault) =>
        $"submit:{(shouldFault ? "fault" : "ok")}:{origin}";

    public static string CreateRequestMessage(string origin, bool shouldFault) =>
        $"request:{(shouldFault ? "fault" : "ok")}:{origin}";

    public static bool ShouldFaultSubmit(string? message) =>
        message?.StartsWith("submit:fault:", StringComparison.OrdinalIgnoreCase) == true;

    public static bool ShouldFaultRequest(string? message) =>
        message?.StartsWith("request:fault:", StringComparison.OrdinalIgnoreCase) == true;
}
