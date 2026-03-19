using TestApp;
using Shouldly;

public class DemoScenarioTests
{
    [Fact]
    public void Submit_fault_messages_are_detected()
    {
        DemoScenario.ShouldFaultSubmit(DemoScenario.CreateSubmitMessage("csharp", true)).ShouldBeTrue();
        DemoScenario.ShouldFaultSubmit(DemoScenario.CreateSubmitMessage("csharp", false)).ShouldBeFalse();
    }

    [Fact]
    public void Request_fault_messages_are_detected()
    {
        DemoScenario.ShouldFaultRequest(DemoScenario.CreateRequestMessage("csharp", true)).ShouldBeTrue();
        DemoScenario.ShouldFaultRequest(DemoScenario.CreateRequestMessage("csharp", false)).ShouldBeFalse();
    }
}
