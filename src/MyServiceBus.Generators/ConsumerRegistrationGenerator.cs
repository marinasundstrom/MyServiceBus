using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MyServiceBus.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ConsumerRegistrationGenerator : IIncrementalGenerator
{
    private const string ConsumerAttributeMetadataName = "MyServiceBus.ConsumerAttribute";
    private static readonly DiagnosticDescriptor InvalidConsumerMethod = new(
        "MSBGEN001",
        "Invalid consumer method",
        "Consumer method '{0}' is invalid: {1}",
        "MyServiceBus.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceRegistrations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (generatorContext, cancellationToken) => GetInterfaceRegistrations(generatorContext, cancellationToken))
            .Where(static registrations => !registrations.IsDefaultOrEmpty)
            .SelectMany(static (registrations, _) => registrations);

        var methodRegistrations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax,
                static (generatorContext, cancellationToken) => GetMethodRegistration(generatorContext, cancellationToken))
            .Where(static registration => registration is not null)
            .Select(static (registration, _) => registration!);

        var diagnostics = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax,
                static (generatorContext, cancellationToken) => GetMethodDiagnostic(generatorContext, cancellationToken))
            .Where(static diagnostic => diagnostic is not null)
            .Select(static (diagnostic, _) => diagnostic!);

        context.RegisterSourceOutput(diagnostics, static (sourceContext, diagnostic) =>
            sourceContext.ReportDiagnostic(diagnostic));

        context.RegisterSourceOutput(
            interfaceRegistrations.Collect().Combine(methodRegistrations.Collect()),
            static (sourceContext, registrations) =>
                EmitCatalog(sourceContext, registrations.Left, registrations.Right));
    }

    private static Diagnostic? GetMethodDiagnostic(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method
            || method.ContainingType is null)
        {
            return null;
        }

        var methodAttribute = FindConsumerAttribute(method);
        var typeAttribute = ImplementsGenericConsumer(method.ContainingType)
            ? null
            : FindConsumerAttribute(method.ContainingType);
        if (methodAttribute is null && typeAttribute is null)
            return null;

        string? reason = null;
        if (method.MethodKind != MethodKind.Ordinary || method.IsAbstract || method.IsGenericMethod)
            reason = "the method must be a non-generic concrete ordinary method";
        else if (!IsAccessibleFromGeneratedCode(method) || !IsAccessibleFromGeneratedCode(method.ContainingType))
            reason = "the method and its declaring type must be accessible from generated code";
        else if (!TryGetReturnKind(method.ReturnType, out _))
            reason = "the return type must be void, Task, or ValueTask";
        else if (method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
            reason = "parameters cannot be passed by reference";
        else
        {
            ITypeSymbol? messageType = null;
            foreach (var parameter in method.Parameters)
            {
                if (GetFrameworkBinding(parameter.Type) == ParameterBinding.None)
                {
                    messageType = parameter.Type;
                    break;
                }
            }

            if (messageType is null || !messageType.IsReferenceType)
                reason = "a reference-type message parameter is required";
            else if (method.Parameters.Any(parameter =>
                parameter.Type is INamedTypeSymbol named
                && IsGenericConsumeContext(named)
                && !SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], messageType)))
            {
                reason = $"the generic consume context must use message type {messageType.ToDisplayString()}";
            }
        }

        return reason is null
            ? null
            : Diagnostic.Create(
                InvalidConsumerMethod,
                declaration.Identifier.GetLocation(),
                method.ToDisplayString(),
                reason);
    }

    private static ImmutableArray<InterfaceRegistration> GetInterfaceRegistrations(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol type
            || type.TypeKind != TypeKind.Class
            || type.IsAbstract
            || !IsAccessibleFromGeneratedCode(type))
        {
            return ImmutableArray<InterfaceRegistration>.Empty;
        }

        var registrations = ImmutableArray.CreateBuilder<InterfaceRegistration>();
        foreach (var implementedInterface in type.AllInterfaces)
        {
            if (implementedInterface.OriginalDefinition.MetadataName != "IConsumer`1"
                || implementedInterface.OriginalDefinition.ContainingNamespace.ToDisplayString() != "MyServiceBus"
                || implementedInterface.TypeArguments.Length != 1
                || implementedInterface.TypeArguments[0] is not INamedTypeSymbol messageType)
            {
                continue;
            }

            registrations.Add(new InterfaceRegistration(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                GetEndpointName(FindConsumerAttribute(type))));
        }

        return registrations.ToImmutable();
    }

    private static MethodRegistration? GetMethodRegistration(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method
            || method.MethodKind != MethodKind.Ordinary
            || method.IsAbstract
            || method.IsGenericMethod
            || !IsAccessibleFromGeneratedCode(method)
            || method.ContainingType is null
            || !IsAccessibleFromGeneratedCode(method.ContainingType))
        {
            return null;
        }

        var methodAttribute = FindConsumerAttribute(method);
        var typeAttribute = FindConsumerAttribute(method.ContainingType);
        if (methodAttribute is null && ImplementsGenericConsumer(method.ContainingType))
            typeAttribute = null;
        if (methodAttribute is null && typeAttribute is null)
            return null;

        if (!TryGetReturnKind(method.ReturnType, out var returnKind))
            return null;

        var parameters = ImmutableArray.CreateBuilder<MethodParameter>();
        ITypeSymbol? messageType = null;
        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
                return null;

            var binding = GetFrameworkBinding(parameter.Type);
            if (binding == ParameterBinding.None)
            {
                if (messageType is null)
                {
                    messageType = parameter.Type;
                    binding = ParameterBinding.Message;
                }
                else
                {
                    binding = ParameterBinding.Service;
                }
            }

            parameters.Add(new MethodParameter(
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                binding));
        }

        if (messageType is null || !messageType.IsReferenceType)
            return null;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is INamedTypeSymbol named
                && IsGenericConsumeContext(named)
                && !SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], messageType))
            {
                return null;
            }
        }

        var methodEndpointName = GetEndpointName(methodAttribute);
        var typeEndpointName = GetEndpointName(typeAttribute);
        var endpointName = methodEndpointName
            ?? typeEndpointName
            ?? (methodAttribute is not null ? method.Name : method.ContainingType.Name);
        var endpointNameIsExplicit = methodEndpointName is not null || typeEndpointName is not null;
        var endpointNameFormatterType = endpointNameIsExplicit || methodAttribute is not null
            ? null
            : method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new MethodRegistration(
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            method.Name,
            messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            endpointName,
            endpointNameIsExplicit,
            endpointNameFormatterType,
            method.IsStatic,
            returnKind,
            parameters.ToImmutable());
    }

    private static AttributeData? FindConsumerAttribute(ISymbol symbol)
        => symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == ConsumerAttributeMetadataName);

    private static bool ImplementsGenericConsumer(INamedTypeSymbol type)
        => type.AllInterfaces.Any(static implementedInterface =>
            implementedInterface.OriginalDefinition.MetadataName == "IConsumer`1"
            && implementedInterface.OriginalDefinition.ContainingNamespace.ToDisplayString() == "MyServiceBus");

    private static string? GetEndpointName(AttributeData? attribute)
        => attribute is { ConstructorArguments.Length: 1 }
            ? attribute.ConstructorArguments[0].Value as string
            : null;

    private static ParameterBinding GetFrameworkBinding(ITypeSymbol type)
    {
        var displayName = type.ToDisplayString();
        if (displayName == "System.Threading.CancellationToken")
            return ParameterBinding.CancellationToken;
        if (displayName == "MyServiceBus.ConsumeContext")
            return ParameterBinding.ConsumeContext;
        if (type is INamedTypeSymbol named && IsGenericConsumeContext(named))
            return ParameterBinding.ConsumeContext;
        return ParameterBinding.None;
    }

    private static bool IsGenericConsumeContext(INamedTypeSymbol type)
        => type.OriginalDefinition.MetadataName == "ConsumeContext`1"
            && type.OriginalDefinition.ContainingNamespace.ToDisplayString() == "MyServiceBus";

    private static bool TryGetReturnKind(ITypeSymbol returnType, out ReturnKind returnKind)
    {
        switch (returnType.ToDisplayString())
        {
            case "void":
                returnKind = ReturnKind.Void;
                return true;
            case "System.Threading.Tasks.Task":
                returnKind = ReturnKind.Task;
                return true;
            case "System.Threading.Tasks.ValueTask":
                returnKind = ReturnKind.ValueTask;
                return true;
            default:
                returnKind = default;
                return false;
        }
    }

    private static bool IsAccessibleFromGeneratedCode(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    private static void EmitCatalog(
        SourceProductionContext context,
        ImmutableArray<InterfaceRegistration> discoveredInterfaces,
        ImmutableArray<MethodRegistration> discoveredMethods)
    {
        var interfaces = discoveredInterfaces
            .Distinct()
            .OrderBy(static registration => registration.ConsumerType, StringComparer.Ordinal)
            .ThenBy(static registration => registration.MessageType, StringComparer.Ordinal)
            .ToArray();
        var methods = discoveredMethods
            .OrderBy(static registration => registration.DeclaringType, StringComparer.Ordinal)
            .ThenBy(static registration => registration.MethodName, StringComparer.Ordinal)
            .ThenBy(static registration => registration.MessageType, StringComparer.Ordinal)
            .ToArray();

        var source = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable

            namespace MyServiceBus.Generated;

            [global::System.CodeDom.Compiler.GeneratedCode("MyServiceBus.Generators", "1.0.0")]
            public static class GeneratedConsumerRegistrationExtensions
            {
                public static global::MyServiceBus.IBusRegistrationConfigurator AddGeneratedConsumers(
                    this global::MyServiceBus.IBusRegistrationConfigurator configurator)
                {
                    global::System.ArgumentNullException.ThrowIfNull(configurator);

            """);

        foreach (var registration in interfaces)
        {
            source.Append("        configurator.AddConsumer<")
                .Append(registration.ConsumerType)
                .Append(", ")
                .Append(registration.MessageType)
                .Append(">(");
            if (registration.EndpointName is not null)
                source.Append(SymbolDisplay.FormatLiteral(registration.EndpointName, quote: true));
            source.AppendLine(");");
        }

        foreach (var declaringType in methods
            .Where(static method => !method.IsStatic)
            .Select(static method => method.DeclaringType)
            .Distinct(StringComparer.Ordinal))
        {
            source.Append("        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<")
                .Append(declaringType)
                .AppendLine(">(configurator.Services);");
        }

        for (var index = 0; index < methods.Length; index++)
        {
            source.Append("        configurator.AddGeneratedConsumer<global::MyServiceBus.Generated.__MethodConsumer")
                .Append(index)
                .Append(", ")
                .Append(methods[index].MessageType)
                .Append(">(")
                .Append(SymbolDisplay.FormatLiteral(methods[index].EndpointName, quote: true))
                .Append(", ");
            if (methods[index].EndpointNameFormatterType is null)
                source.Append("null");
            else
                source.Append("typeof(").Append(methods[index].EndpointNameFormatterType).Append(')');
            source.Append(", ")
                .Append(methods[index].EndpointNameIsExplicit ? "true" : "false")
                .Append(", static provider => new global::MyServiceBus.Generated.__MethodConsumer")
                .Append(index)
                .Append('(');
            var factoryArguments = new List<string>();
            if (!methods[index].IsStatic)
            {
                factoryArguments.Add(
                    "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<"
                    + methods[index].DeclaringType
                    + ">(provider)");
            }
            factoryArguments.AddRange(methods[index].Parameters
                .Where(static parameter => parameter.Binding == ParameterBinding.Service)
                .Select(static parameter =>
                    "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<"
                    + parameter.Type
                    + ">(provider)"));
            source.Append(string.Join(", ", factoryArguments)).AppendLine("));");
        }

        source.AppendLine(
            """

                    return configurator;
                }
            }
            """);

        for (var index = 0; index < methods.Length; index++)
            EmitMethodAdapter(source, methods[index], index);

        context.AddSource("GeneratedConsumerRegistrationExtensions.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void EmitMethodAdapter(StringBuilder source, MethodRegistration method, int index)
    {
        var services = method.Parameters
            .Select((parameter, parameterIndex) => new IndexedParameter(parameter, parameterIndex))
            .Where(static item => item.Parameter.Binding == ParameterBinding.Service)
            .ToArray();

        source.AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
        source.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"MyServiceBus.Generators\", \"1.0.0\")]");
        source.Append("internal sealed class __MethodConsumer")
            .Append(index)
            .Append(" : global::MyServiceBus.IConsumer<")
            .Append(method.MessageType)
            .AppendLine(">");
        source.AppendLine("{");

        if (!method.IsStatic)
            source.Append("    private readonly ").Append(method.DeclaringType).AppendLine(" _target;");
        foreach (var service in services)
        {
            source.Append("    private readonly ")
                .Append(service.Parameter.Type)
                .Append(" _service")
                .Append(service.Index)
                .AppendLine(";");
        }

        if (!method.IsStatic || services.Length > 0)
        {
            source.Append("    public __MethodConsumer").Append(index).Append('(');
            var constructorParameters = new List<string>();
            if (!method.IsStatic)
                constructorParameters.Add(method.DeclaringType + " target");
            constructorParameters.AddRange(services.Select(service =>
                service.Parameter.Type + " service" + service.Index));
            source.Append(string.Join(", ", constructorParameters)).AppendLine(")");
            source.AppendLine("    {");
            if (!method.IsStatic)
                source.AppendLine("        _target = target;");
            foreach (var service in services)
            {
                source.Append("        _service")
                    .Append(service.Index)
                    .Append(" = service")
                    .Append(service.Index)
                    .AppendLine(";");
            }

            source.AppendLine("    }");
            source.AppendLine();
        }

        source.Append("    public global::System.Threading.Tasks.Task Consume(global::MyServiceBus.ConsumeContext<")
            .Append(method.MessageType)
            .AppendLine("> context)");
        source.AppendLine("    {");
        source.Append(method.ReturnKind == ReturnKind.Void ? "        " : "        return ");
        source.Append(method.IsStatic ? method.DeclaringType : "_target")
            .Append('.')
            .Append(method.MethodName)
            .Append('(');
        source.Append(string.Join(", ", method.Parameters.Select((parameter, parameterIndex) =>
            parameter.Binding switch
            {
                ParameterBinding.Message => "context.Message",
                ParameterBinding.ConsumeContext => "context",
                ParameterBinding.CancellationToken => "context.CancellationToken",
                ParameterBinding.Service => "_service" + parameterIndex,
                _ => throw new InvalidOperationException()
            })));
        source.Append(')');
        if (method.ReturnKind == ReturnKind.ValueTask)
            source.Append(".AsTask()");
        source.AppendLine(";");
        if (method.ReturnKind == ReturnKind.Void)
            source.AppendLine("        return global::System.Threading.Tasks.Task.CompletedTask;");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private readonly struct InterfaceRegistration : IEquatable<InterfaceRegistration>
    {
        public InterfaceRegistration(string consumerType, string messageType, string? endpointName)
        {
            ConsumerType = consumerType;
            MessageType = messageType;
            EndpointName = endpointName;
        }

        public string ConsumerType { get; }
        public string MessageType { get; }
        public string? EndpointName { get; }
        public bool Equals(InterfaceRegistration other) => ConsumerType == other.ConsumerType && MessageType == other.MessageType && EndpointName == other.EndpointName;
        public override bool Equals(object? obj) => obj is InterfaceRegistration other && Equals(other);
        public override int GetHashCode() => ((ConsumerType.GetHashCode() * 397) ^ MessageType.GetHashCode()) * 397 ^ (EndpointName?.GetHashCode() ?? 0);
    }

    private sealed class MethodRegistration
    {
        public MethodRegistration(
            string declaringType,
            string methodName,
            string messageType,
            string endpointName,
            bool endpointNameIsExplicit,
            string? endpointNameFormatterType,
            bool isStatic,
            ReturnKind returnKind,
            ImmutableArray<MethodParameter> parameters)
        {
            DeclaringType = declaringType;
            MethodName = methodName;
            MessageType = messageType;
            EndpointName = endpointName;
            EndpointNameIsExplicit = endpointNameIsExplicit;
            EndpointNameFormatterType = endpointNameFormatterType;
            IsStatic = isStatic;
            ReturnKind = returnKind;
            Parameters = parameters;
        }

        public string DeclaringType { get; }
        public string MethodName { get; }
        public string MessageType { get; }
        public string EndpointName { get; }
        public bool EndpointNameIsExplicit { get; }
        public string? EndpointNameFormatterType { get; }
        public bool IsStatic { get; }
        public ReturnKind ReturnKind { get; }
        public ImmutableArray<MethodParameter> Parameters { get; }
    }

    private readonly struct MethodParameter
    {
        public MethodParameter(string type, ParameterBinding binding)
        {
            Type = type;
            Binding = binding;
        }

        public string Type { get; }
        public ParameterBinding Binding { get; }
    }

    private readonly struct IndexedParameter
    {
        public IndexedParameter(MethodParameter parameter, int index)
        {
            Parameter = parameter;
            Index = index;
        }

        public MethodParameter Parameter { get; }
        public int Index { get; }
    }

    private enum ParameterBinding { None, Message, ConsumeContext, CancellationToken, Service }
    private enum ReturnKind { Void, Task, ValueTask }
}
