namespace MyServiceBus.Choreography;

/// <summary>
/// Builds a normalized declaration of application-owned choreography reactions.
/// </summary>
public sealed class ChoreographyBuilder
{
    private readonly string choreographyId;
    private readonly string definitionVersion;
    private readonly string owner;
    private readonly List<ChoreographyStep> steps = new();

    public ChoreographyBuilder(string choreographyId, string definitionVersion, string owner)
    {
        this.choreographyId = Required(choreographyId, nameof(choreographyId));
        this.definitionVersion = Required(definitionVersion, nameof(definitionVersion));
        this.owner = Required(owner, nameof(owner));
    }

    public ChoreographyBuilder Step<TTrigger>(string id, Action<ChoreographyStepBuilder> configure)
        => Step(id, MessageUrn.For(typeof(TTrigger)), configure);

    public ChoreographyBuilder Step(
        string id,
        string triggerMessageUrn,
        Action<ChoreographyStepBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var stepId = Required(id, nameof(id));
        if (steps.Any(step => string.Equals(step.Id, stepId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"A choreography step with ID '{stepId}' is already declared.", nameof(id));
        }

        var builder = new ChoreographyStepBuilder(stepId, Required(triggerMessageUrn, nameof(triggerMessageUrn)));
        configure(builder);
        steps.Add(builder.Build());
        return this;
    }

    public ChoreographyFragment Build()
    {
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("A choreography fragment must declare at least one step.");
        }

        var fragment = new ChoreographyFragment(
            ChoreographyFragment.CurrentSchemaVersion,
            choreographyId,
            definitionVersion,
            owner,
            steps.OrderBy(step => step.Id, StringComparer.Ordinal).ToArray());
        fragment.Validate();
        return fragment;
    }

    internal static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }

        return value;
    }
}

public sealed class ChoreographyStepBuilder
{
    private readonly string id;
    private readonly string triggerMessageUrn;
    private readonly List<ChoreographyOutput> outputs = new();
    private string? ownerComponent;

    internal ChoreographyStepBuilder(string id, string triggerMessageUrn)
    {
        this.id = id;
        this.triggerMessageUrn = triggerMessageUrn;
    }

    public ChoreographyStepBuilder OwnedBy<TComponent>() => OwnedBy(typeof(TComponent).FullName ?? typeof(TComponent).Name);

    public ChoreographyStepBuilder OwnedBy(string component)
    {
        ownerComponent = ChoreographyBuilder.Required(component, nameof(component));
        return this;
    }

    public ChoreographyStepBuilder Sends<TMessage>(
        string destination,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Sends(MessageUrn.For(typeof(TMessage)), destination, configure);

    public ChoreographyStepBuilder Sends(
        string messageUrn,
        string destination,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Add(ChoreographyOperationKind.Send, messageUrn, ChoreographyBuilder.Required(destination, nameof(destination)), configure);

    public ChoreographyStepBuilder Publishes<TMessage>(Action<ChoreographyOutputBuilder>? configure = null)
        => Publishes(MessageUrn.For(typeof(TMessage)), configure);

    public ChoreographyStepBuilder Publishes(
        string messageUrn,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Add(ChoreographyOperationKind.Publish, messageUrn, null, configure);

    public ChoreographyStepBuilder Responds<TMessage>(Action<ChoreographyOutputBuilder>? configure = null)
        => Responds(MessageUrn.For(typeof(TMessage)), configure);

    public ChoreographyStepBuilder Responds(
        string messageUrn,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Add(ChoreographyOperationKind.Respond, messageUrn, null, configure);

    public ChoreographyStepBuilder Schedules<TMessage>(Action<ChoreographyOutputBuilder>? configure = null)
        => Schedules(MessageUrn.For(typeof(TMessage)), null, configure);

    public ChoreographyStepBuilder Schedules<TMessage>(
        string destination,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Schedules(MessageUrn.For(typeof(TMessage)), ChoreographyBuilder.Required(destination, nameof(destination)), configure);

    public ChoreographyStepBuilder Schedules(
        string messageUrn,
        string? destination = null,
        Action<ChoreographyOutputBuilder>? configure = null)
        => Add(ChoreographyOperationKind.Schedule, messageUrn, destination, configure);

    public ChoreographyStepBuilder Terminates(Action<ChoreographyOutputBuilder>? configure = null)
        => Add(ChoreographyOperationKind.Terminal, null, null, configure);

    internal ChoreographyStep Build()
    {
        if (outputs.Count == 0)
        {
            throw new InvalidOperationException($"Choreography step '{id}' must declare at least one output or terminal outcome.");
        }

        var normalizedOutputs = outputs
            .OrderBy(output => output.Kind)
            .ThenBy(output => output.MessageUrn, StringComparer.Ordinal)
            .ThenBy(output => output.Destination, StringComparer.Ordinal)
            .ThenBy(output => output.Requirement)
            .ThenBy(output => output.MinCount)
            .ThenBy(output => output.MaxCount)
            .ThenBy(output => output.WithinMilliseconds)
            .ToArray();

        return new ChoreographyStep(id, triggerMessageUrn, ownerComponent, normalizedOutputs);
    }

    private ChoreographyStepBuilder Add(
        ChoreographyOperationKind kind,
        string? messageUrn,
        string? destination,
        Action<ChoreographyOutputBuilder>? configure)
    {
        if (kind != ChoreographyOperationKind.Terminal)
        {
            messageUrn = ChoreographyBuilder.Required(messageUrn!, nameof(messageUrn));
        }

        var builder = new ChoreographyOutputBuilder(kind, messageUrn, destination);
        configure?.Invoke(builder);
        outputs.Add(builder.Build());
        return this;
    }
}

public sealed class ChoreographyOutputBuilder
{
    private readonly ChoreographyOperationKind kind;
    private readonly string? messageUrn;
    private readonly string? destination;
    private ChoreographyRequirement requirement = ChoreographyRequirement.Expected;
    private int? minCount;
    private int? maxCount;
    private long? withinMilliseconds;

    internal ChoreographyOutputBuilder(ChoreographyOperationKind kind, string? messageUrn, string? destination)
    {
        this.kind = kind;
        this.messageUrn = messageUrn;
        this.destination = destination;
    }

    public ChoreographyOutputBuilder Informational()
    {
        requirement = ChoreographyRequirement.Informational;
        return this;
    }

    public ChoreographyOutputBuilder Optional()
    {
        requirement = ChoreographyRequirement.Optional;
        return this;
    }

    public ChoreographyOutputBuilder Expected()
    {
        requirement = ChoreographyRequirement.Expected;
        return this;
    }

    public ChoreographyOutputBuilder AtLeast(int count)
    {
        minCount = NonNegative(count, nameof(count));
        return this;
    }

    public ChoreographyOutputBuilder AtMost(int count)
    {
        maxCount = NonNegative(count, nameof(count));
        return this;
    }

    public ChoreographyOutputBuilder Exactly(int count)
    {
        minCount = maxCount = NonNegative(count, nameof(count));
        return this;
    }

    public ChoreographyOutputBuilder Within(TimeSpan duration)
    {
        var milliseconds = checked((long)duration.TotalMilliseconds);
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The time expectation must be at least one millisecond.");
        }

        withinMilliseconds = milliseconds;
        return this;
    }

    internal ChoreographyOutput Build()
    {
        if (minCount > maxCount)
        {
            throw new InvalidOperationException("The minimum output count cannot exceed the maximum output count.");
        }

        if (kind == ChoreographyOperationKind.Terminal &&
            (minCount is not null || maxCount is not null || withinMilliseconds is not null))
        {
            throw new InvalidOperationException("A terminal outcome cannot declare output count or timing expectations.");
        }

        return new ChoreographyOutput(
            kind,
            messageUrn,
            destination,
            requirement,
            minCount,
            maxCount,
            withinMilliseconds);
    }

    private static int NonNegative(int count, string parameterName)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The output count cannot be negative.");
        }

        return count;
    }
}
