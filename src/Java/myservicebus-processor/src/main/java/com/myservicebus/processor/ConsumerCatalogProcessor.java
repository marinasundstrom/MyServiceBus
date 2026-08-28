package com.myservicebus.processor;

import java.io.IOException;
import java.io.Writer;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import javax.annotation.processing.AbstractProcessor;
import javax.annotation.processing.FilerException;
import javax.annotation.processing.RoundEnvironment;
import javax.annotation.processing.SupportedAnnotationTypes;
import javax.annotation.processing.SupportedSourceVersion;
import javax.lang.model.SourceVersion;
import javax.lang.model.element.Element;
import javax.lang.model.element.ElementKind;
import javax.lang.model.element.ExecutableElement;
import javax.lang.model.element.Modifier;
import javax.lang.model.element.TypeElement;
import javax.lang.model.element.VariableElement;
import javax.lang.model.type.DeclaredType;
import javax.lang.model.type.TypeKind;
import javax.lang.model.type.TypeMirror;
import javax.tools.Diagnostic;
import javax.tools.JavaFileObject;

import com.myservicebus.MessageConsumer;

@SupportedAnnotationTypes("*")
@SupportedSourceVersion(SourceVersion.RELEASE_17)
public final class ConsumerCatalogProcessor extends AbstractProcessor {
    private static final String GENERATED_TYPE = "com.myservicebus.generated.GeneratedConsumerCatalog";
    private final Map<String, InterfaceConsumer> interfaceConsumers = new LinkedHashMap<>();
    private final Map<String, MethodConsumer> methodConsumers = new LinkedHashMap<>();
    private final List<Element> origins = new ArrayList<>();
    private boolean generated;

    @Override
    public boolean process(Set<? extends TypeElement> annotations, RoundEnvironment roundEnvironment) {
        if (!roundEnvironment.processingOver()) {
            for (Element root : roundEnvironment.getRootElements()) {
                if (root instanceof TypeElement type) {
                    scan(type);
                }
            }
            if (!generated && (!interfaceConsumers.isEmpty() || !methodConsumers.isEmpty())) {
                generated = true;
                generateCatalog();
            }
            return false;
        }
        return false;
    }

    private void scan(TypeElement type) {
        if ((type.getKind() == ElementKind.CLASS || type.getKind() == ElementKind.RECORD)
                && isPackageAccessible(type)) {
            TypeMirror messageType = findConsumerMessage(type.asType());
            boolean interfaceConsumer = messageType != null;
            if (interfaceConsumer) {
                if (isClassLiteralType(messageType)) {
                    String key = type.getQualifiedName() + "|" + messageType;
                    interfaceConsumers.putIfAbsent(key, new InterfaceConsumer(
                            type,
                            sourceName(type),
                            classLiteralName(messageType),
                            endpointExpression(type.getAnnotation(MessageConsumer.class), type, null),
                            annotationValue(type.getAnnotation(MessageConsumer.class)) != null));
                    origins.add(type);
                } else {
                    error(type, "Consumer message type must have a concrete class literal");
                }
            }

            MessageConsumer typeAnnotation = interfaceConsumer ? null : type.getAnnotation(MessageConsumer.class);
            for (Element enclosed : type.getEnclosedElements()) {
                if (enclosed instanceof ExecutableElement method && method.getKind() == ElementKind.METHOD) {
                    MessageConsumer methodAnnotation = method.getAnnotation(MessageConsumer.class);
                    if (methodAnnotation != null || typeAnnotation != null) {
                        MethodConsumer registration = analyzeMethod(type, method, methodAnnotation, typeAnnotation);
                        if (registration != null) {
                            methodConsumers.putIfAbsent(registration.key(), registration);
                            origins.add(method);
                        }
                    }
                }
            }
        }

        for (Element enclosed : type.getEnclosedElements()) {
            if (enclosed instanceof TypeElement nested) {
                scan(nested);
            }
        }
    }

    private MethodConsumer analyzeMethod(
            TypeElement declaringType,
            ExecutableElement method,
            MessageConsumer methodAnnotation,
            MessageConsumer typeAnnotation) {
        if (!method.getModifiers().contains(Modifier.PUBLIC)
                || method.getModifiers().contains(Modifier.ABSTRACT)
                || !method.getTypeParameters().isEmpty()) {
            error(method, "Consumer method must be public, concrete, and non-generic");
            return null;
        }

        ReturnKind returnKind = returnKind(method.getReturnType());
        if (returnKind == null) {
            error(method, "Consumer method return type must be void, CompletableFuture, or CompletionStage with an optional concrete response type");
            return null;
        }

        TypeMirror messageType = null;
        List<Parameter> parameters = new ArrayList<>();
        for (VariableElement parameter : method.getParameters()) {
            Binding binding;
            if (isType(parameter.asType(), "com.myservicebus.ConsumeContext")) {
                binding = Binding.CONTEXT;
            } else if (isType(parameter.asType(), "com.myservicebus.tasks.CancellationToken")) {
                binding = Binding.CANCELLATION_TOKEN;
            } else if (messageType == null) {
                messageType = parameter.asType();
                binding = Binding.MESSAGE;
            } else {
                binding = Binding.SERVICE;
            }
            parameters.add(new Parameter(classLiteralName(parameter.asType()), binding));
        }

        if (messageType == null || !isClassLiteralType(messageType)) {
            error(method, "Consumer method requires a concrete reference-type message parameter");
            return null;
        }
        String methodEndpoint = annotationValue(methodAnnotation);
        String typeEndpoint = annotationValue(typeAnnotation);
        String endpoint = endpointExpression(methodAnnotation, declaringType, method);
        if (methodEndpoint == null && typeEndpoint != null) {
            endpoint = quote(typeEndpoint);
        }
        boolean endpointNameExplicit = methodEndpoint != null || typeEndpoint != null;
        String endpointNameFormatterType = endpointNameExplicit || methodAnnotation != null
                ? "null"
                : sourceName(declaringType) + ".class";

        return new MethodConsumer(
                declaringType,
                sourceName(declaringType),
                method.getSimpleName().toString(),
                classLiteralName(messageType),
                endpoint,
                endpointNameExplicit,
                endpointNameFormatterType,
                method.getModifiers().contains(Modifier.STATIC),
                returnKind,
                parameters);
    }

    private void generateCatalog() {
        List<InterfaceConsumer> interfaces = interfaceConsumers.values().stream()
                .sorted(Comparator.comparing(InterfaceConsumer::key))
                .toList();
        List<MethodConsumer> methods = methodConsumers.values().stream()
                .sorted(Comparator.comparing(MethodConsumer::key))
                .toList();

        Map<String, List<InterfaceConsumer>> interfacesByPackage = new LinkedHashMap<>();
        for (InterfaceConsumer consumer : interfaces) {
            interfacesByPackage.computeIfAbsent(packageName(consumer.origin()), ignored -> new ArrayList<>())
                    .add(consumer);
        }
        Map<String, List<MethodConsumer>> methodsByPackage = new LinkedHashMap<>();
        for (MethodConsumer method : methods) {
            methodsByPackage.computeIfAbsent(packageName(method.origin()), ignored -> new ArrayList<>())
                    .add(method);
        }
        List<String> packages = new ArrayList<>();
        packages.addAll(interfacesByPackage.keySet());
        for (String packageName : methodsByPackage.keySet()) {
            if (!packages.contains(packageName)) {
                packages.add(packageName);
            }
        }
        packages.sort(String::compareTo);

        for (String packageName : packages) {
            generatePackageCatalog(
                    packageName,
                    interfacesByPackage.getOrDefault(packageName, List.of()),
                    methodsByPackage.getOrDefault(packageName, List.of()));
        }

        try {
            JavaFileObject file = processingEnv.getFiler().createSourceFile(
                    GENERATED_TYPE,
                    origins.toArray(Element[]::new));
            try (Writer writer = file.openWriter()) {
                writer.write("package com.myservicebus.generated;\n\n");
                writer.write("@javax.annotation.processing.Generated(\"com.myservicebus.processor.ConsumerCatalogProcessor\")\n");
                writer.write("public final class GeneratedConsumerCatalog implements com.myservicebus.ConsumerCatalog {\n");
                writer.write("    public static final GeneratedConsumerCatalog INSTANCE = new GeneratedConsumerCatalog();\n\n");
                writer.write("    private GeneratedConsumerCatalog() {}\n\n");
                writer.write("    @Override\n");
                writer.write("    public void register(com.myservicebus.BusRegistrationConfigurator configurator) {\n");
                for (String packageName : packages) {
                    writer.write("        " + packageName
                            + ".MyServiceBusGeneratedConsumerCatalog.register(configurator);\n");
                }
                writer.write("    }\n");
                writer.write("}\n");
            }
        } catch (FilerException duplicate) {
            error(null, "Could not generate consumer catalog because the type already exists: " + duplicate.getMessage());
        } catch (IOException exception) {
            error(null, "Could not generate consumer catalog: " + exception.getMessage());
        }
    }

    private void generatePackageCatalog(
            String packageName,
            List<InterfaceConsumer> interfaces,
            List<MethodConsumer> methods) {
        String generatedType = packageName + ".MyServiceBusGeneratedConsumerCatalog";
        List<Element> packageOrigins = new ArrayList<>();
        interfaces.forEach(consumer -> packageOrigins.add(consumer.origin()));
        methods.forEach(method -> packageOrigins.add(method.origin()));
        try {
            JavaFileObject file = processingEnv.getFiler().createSourceFile(
                    generatedType,
                    packageOrigins.toArray(Element[]::new));
            try (Writer writer = file.openWriter()) {
                writer.write("package " + packageName + ";\n\n");
                writer.write("@javax.annotation.processing.Generated(\"com.myservicebus.processor.ConsumerCatalogProcessor\")\n");
                writer.write("@SuppressWarnings({\"rawtypes\", \"unchecked\"})\n");
                writer.write("public final class MyServiceBusGeneratedConsumerCatalog {\n");
                writer.write("    private MyServiceBusGeneratedConsumerCatalog() {}\n\n");
                writer.write("    public static void register(com.myservicebus.BusRegistrationConfigurator configurator) {\n");
                for (InterfaceConsumer consumer : interfaces) {
                    writer.write("        configurator.addConsumer(");
                    writer.write("(Class) " + consumer.consumerType() + ".class, ");
                    writer.write("(Class) " + consumer.messageType() + ".class");
                    if (consumer.endpointNameExplicit()) {
                        writer.write(", " + consumer.endpointExpression() + ", null");
                    }
                    writer.write(");\n");
                }
                methods.stream()
                        .filter(method -> !method.isStatic())
                        .map(MethodConsumer::declaringType)
                        .distinct()
                        .forEach(type -> writeUnchecked(writer,
                                "        configurator.getServiceCollection().addScoped(" + type + ".class);\n"));
                for (MethodConsumer method : methods) {
                    writeMethodRegistration(writer, method);
                }
                writer.write("    }\n");
                writer.write("}\n");
            }
        } catch (IOException exception) {
            error(null, "Could not generate package consumer catalog: " + exception.getMessage());
        }
    }

    private void writeMethodRegistration(Writer writer, MethodConsumer method) throws IOException {
        writer.write("        configurator.addConsumerMethod(");
        writer.write(method.declaringType() + ".class, " + method.messageType() + ".class, ");
        writer.write(method.endpointExpression() + ", ");
        writer.write(method.endpointNameExplicit() + ", ");
        writer.write(method.endpointNameFormatterType() + ", (provider, context) -> {\n");
        String target = method.isStatic()
                ? method.declaringType()
                : "provider.getRequiredService(" + method.declaringType() + ".class)";
        String arguments = String.join(", ", method.parameters().stream().map(parameter -> switch (parameter.binding()) {
            case MESSAGE -> "context.getMessage()";
            case CONTEXT -> "context";
            case CANCELLATION_TOKEN -> "context.getCancellationToken()";
            case SERVICE -> "provider.getRequiredService(" + parameter.type() + ".class)";
        }).toList());
        String invocation = target + "." + method.methodName() + "(" + arguments + ")";
        switch (method.returnKind()) {
            case VOID -> {
                writer.write("            " + invocation + ";\n");
                writer.write("            return java.util.concurrent.CompletableFuture.completedFuture(null);\n");
            }
            case FUTURE, STAGE -> writer.write(
                    "            return " + invocation
                            + ".thenApply(ignored -> (Void) null).toCompletableFuture();\n");
            case FUTURE_RESPONSE, STAGE_RESPONSE -> writer.write(
                    "            return " + invocation
                            + ".thenCompose(response -> context.respond(response, context.getCancellationToken()))"
                            + ".toCompletableFuture();\n");
        }
        writer.write("        });\n");
    }

    private TypeMirror findConsumerMessage(TypeMirror type) {
        TypeElement consumerElement = processingEnv.getElementUtils().getTypeElement("com.myservicebus.Consumer");
        if (consumerElement == null) {
            return null;
        }
        TypeMirror consumerErasure = processingEnv.getTypeUtils().erasure(consumerElement.asType());
        for (TypeMirror supertype : processingEnv.getTypeUtils().directSupertypes(type)) {
            if (processingEnv.getTypeUtils().isSameType(processingEnv.getTypeUtils().erasure(supertype), consumerErasure)
                    && supertype instanceof DeclaredType declared
                    && declared.getTypeArguments().size() == 1) {
                return declared.getTypeArguments().get(0);
            }
            TypeMirror nested = findConsumerMessage(supertype);
            if (nested != null) {
                return nested;
            }
        }
        return null;
    }

    private boolean isType(TypeMirror type, String qualifiedName) {
        TypeElement expected = processingEnv.getElementUtils().getTypeElement(qualifiedName);
        return expected != null && processingEnv.getTypeUtils().isSameType(
                processingEnv.getTypeUtils().erasure(type),
                processingEnv.getTypeUtils().erasure(expected.asType()));
    }

    private ReturnKind returnKind(TypeMirror type) {
        if (type.getKind() == TypeKind.VOID) {
            return ReturnKind.VOID;
        }
        if (isType(type, "java.util.concurrent.CompletableFuture")) {
            return asyncReturnKind(type, ReturnKind.FUTURE, ReturnKind.FUTURE_RESPONSE);
        }
        TypeElement stage = processingEnv.getElementUtils().getTypeElement("java.util.concurrent.CompletionStage");
        return stage != null && processingEnv.getTypeUtils().isAssignable(
                processingEnv.getTypeUtils().erasure(type),
                processingEnv.getTypeUtils().erasure(stage.asType()))
                        ? asyncReturnKind(type, ReturnKind.STAGE, ReturnKind.STAGE_RESPONSE)
                        : null;
    }

    private ReturnKind asyncReturnKind(TypeMirror type, ReturnKind oneWayKind, ReturnKind responseKind) {
        if (!(type instanceof DeclaredType declared) || declared.getTypeArguments().isEmpty()) {
            return oneWayKind;
        }
        TypeMirror responseType = declared.getTypeArguments().get(0);
        if (isType(responseType, "java.lang.Void")) {
            return oneWayKind;
        }
        return isClassLiteralType(responseType) ? responseKind : null;
    }

    private boolean isClassLiteralType(TypeMirror type) {
        return type.getKind() == TypeKind.DECLARED || type.getKind() == TypeKind.ARRAY;
    }

    private String classLiteralName(TypeMirror type) {
        return processingEnv.getTypeUtils().erasure(type).toString();
    }

    private boolean isPackageAccessible(TypeElement type) {
        for (Element current = type; current instanceof TypeElement; current = current.getEnclosingElement()) {
            if (current.getModifiers().contains(Modifier.PRIVATE)
                    || current.getModifiers().contains(Modifier.PROTECTED)) {
                return false;
            }
        }
        return true;
    }

    private String packageName(Element element) {
        return processingEnv.getElementUtils().getPackageOf(element).getQualifiedName().toString();
    }

    private String endpointExpression(MessageConsumer annotation, TypeElement type, ExecutableElement method) {
        String value = annotationValue(annotation);
        if (value != null) {
            return quote(value);
        }
        if (method != null && annotation != null) {
            return quote(method.getSimpleName().toString());
        }
        return "com.myservicebus.DefaultEndpointNameFormatter.INSTANCE.format(" + sourceName(type) + ".class)";
    }

    private static String annotationValue(MessageConsumer annotation) {
        return annotation == null || annotation.value().isBlank() ? null : annotation.value();
    }

    private static String quote(String value) {
        return "\"" + value.replace("\\", "\\\\").replace("\"", "\\\"") + "\"";
    }

    private static String sourceName(TypeElement type) {
        return type.getQualifiedName().toString();
    }

    private static String sourceName(TypeMirror type) {
        return type.toString();
    }

    private void error(Element element, String message) {
        if (element == null) {
            processingEnv.getMessager().printMessage(Diagnostic.Kind.ERROR, message);
        } else {
            processingEnv.getMessager().printMessage(Diagnostic.Kind.ERROR, message, element);
        }
    }

    private static void writeUnchecked(Writer writer, String value) {
        try {
            writer.write(value);
        } catch (IOException exception) {
            throw new IllegalStateException(exception);
        }
    }

    private record InterfaceConsumer(
            Element origin,
            String consumerType,
            String messageType,
            String endpointExpression,
            boolean endpointNameExplicit) {
        String key() {
            return consumerType + "|" + messageType;
        }
    }

    private record MethodConsumer(
            Element origin,
            String declaringType,
            String methodName,
            String messageType,
            String endpointExpression,
            boolean endpointNameExplicit,
            String endpointNameFormatterType,
            boolean isStatic,
            ReturnKind returnKind,
            List<Parameter> parameters) {
        String key() {
            return declaringType + "|" + methodName + "|" + messageType;
        }
    }

    private record Parameter(String type, Binding binding) {
    }

    private enum Binding {
        MESSAGE,
        CONTEXT,
        CANCELLATION_TOKEN,
        SERVICE
    }

    private enum ReturnKind {
        VOID,
        FUTURE,
        STAGE,
        FUTURE_RESPONSE,
        STAGE_RESPONSE
    }
}
