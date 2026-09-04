package com.myservicebus.kotlin.processor

import com.google.devtools.ksp.getAllSuperTypes
import com.google.devtools.ksp.validate
import com.google.devtools.ksp.processing.CodeGenerator
import com.google.devtools.ksp.processing.Dependencies
import com.google.devtools.ksp.processing.KSPLogger
import com.google.devtools.ksp.processing.Resolver
import com.google.devtools.ksp.processing.SymbolProcessor
import com.google.devtools.ksp.processing.SymbolProcessorEnvironment
import com.google.devtools.ksp.processing.SymbolProcessorProvider
import com.google.devtools.ksp.symbol.KSAnnotated
import com.google.devtools.ksp.symbol.KSClassDeclaration
import com.google.devtools.ksp.symbol.ClassKind
import com.google.devtools.ksp.symbol.KSDeclaration
import com.google.devtools.ksp.symbol.KSFunctionDeclaration
import com.google.devtools.ksp.symbol.Modifier
import com.google.devtools.ksp.symbol.KSValueParameter
import com.google.devtools.ksp.symbol.Nullability

class KotlinConsumerCatalogProcessorProvider : SymbolProcessorProvider {
    override fun create(environment: SymbolProcessorEnvironment): SymbolProcessor =
        KotlinConsumerCatalogProcessor(
            environment.codeGenerator,
            environment.logger,
            environment.options[CATALOG_PACKAGE_OPTION] ?: DEFAULT_GENERATED_PACKAGE,
            environment.options[CATALOG_NAME_OPTION] ?: DEFAULT_GENERATED_NAME,
        )

    private companion object {
        const val CATALOG_PACKAGE_OPTION = "myservicebus.catalog.package"
        const val CATALOG_NAME_OPTION = "myservicebus.catalog.name"
        const val DEFAULT_GENERATED_PACKAGE = "com.myservicebus.kotlin.generated"
        const val DEFAULT_GENERATED_NAME = "GeneratedConsumerCatalog"
    }
}

private class KotlinConsumerCatalogProcessor(
    private val codeGenerator: CodeGenerator,
    private val logger: KSPLogger,
    private val generatedPackage: String,
    private val generatedName: String,
) : SymbolProcessor {
    private var generated = false

    init {
        require(generatedPackage.split('.').all { it.isKotlinIdentifier() }) {
            "myservicebus.catalog.package must be a qualified Kotlin package name"
        }
        require(generatedName.isKotlinIdentifier()) {
            "myservicebus.catalog.name must be a Kotlin type name"
        }
    }

    override fun process(resolver: Resolver): List<KSAnnotated> {
        if (generated) return emptyList()

        val symbols = resolver.getSymbolsWithAnnotation(CONSUMER_FUNCTION).toList()
        val deferred = symbols.filterNot(KSAnnotated::validate)
        if (deferred.isNotEmpty()) return deferred
        val functions = symbols.filterIsInstance<KSFunctionDeclaration>()
            .filter(KSAnnotated::validate)
            .mapNotNull(::analyze)
            .sortedBy(ConsumerFunctionModel::identity)
        val classes = resolver.getAllFiles()
            .flatMap { file -> file.declarations.flatMap(::flatten) }
            .filterIsInstance<KSClassDeclaration>()
            .mapNotNull(::analyzeClass)
            .sortedBy(ConsumerClassModel::qualifiedName)
            .toList()

        if (functions.isNotEmpty() || classes.isNotEmpty()) {
            generate(functions, classes)
            generated = true
        }
        return deferred
    }

    private fun flatten(declaration: KSDeclaration): Sequence<KSDeclaration> = sequence {
        yield(declaration)
        if (declaration is KSClassDeclaration) {
            declaration.declarations.forEach { yieldAll(flatten(it)) }
        }
    }

    private fun analyzeClass(declaration: KSClassDeclaration): ConsumerClassModel? {
        if (declaration.classKind != ClassKind.CLASS || Modifier.ABSTRACT in declaration.modifiers) return null
        val contracts = declaration.getAllSuperTypes().toList()
        val handler = contracts.singleOrNull {
            it.declaration.qualifiedName?.asString() == HANDLER
        }
        val consumer = contracts.singleOrNull {
            it.declaration.qualifiedName?.asString() == CONSUMER
        }
        val contract = handler ?: consumer ?: return null
        if (Modifier.PRIVATE in declaration.modifiers || Modifier.PROTECTED in declaration.modifiers) {
            logger.error("Kotlin consumer class must be public or internal", declaration)
            return null
        }
        if (declaration.typeParameters.isNotEmpty()) {
            logger.error("Kotlin consumer class must not be generic", declaration)
            return null
        }
        val typeArguments = contract.arguments.mapNotNull {
            it.type?.resolve()?.declaration?.qualifiedName?.asString()
        }
        val expectedArguments = if (handler != null) 2 else 1
        if (typeArguments.size != expectedArguments) {
            logger.error("Kotlin consumer contract must use concrete type arguments", declaration)
            return null
        }
        val qualifiedName = declaration.qualifiedName?.asString() ?: return null
        val annotation = declaration.annotations.singleOrNull {
            it.annotationType.resolve().declaration.qualifiedName?.asString() == MESSAGE_CONSUMER
        }
        val configuredEndpoint = annotation?.arguments
            ?.singleOrNull { it.name?.asString() == "value" }
            ?.value as? String ?: ""
        return ConsumerClassModel(
            qualifiedName = qualifiedName,
            messageType = typeArguments.first(),
            endpointName = configuredEndpoint.ifBlank { null },
            endpointNameExplicit = configuredEndpoint.isNotBlank(),
            handler = handler != null,
            origin = declaration.containingFile,
        )
    }

    private fun analyze(function: KSFunctionDeclaration): ConsumerFunctionModel? {
        var valid = true
        fun reject(message: String) {
            logger.error(message, function)
            valid = false
        }

        if (function.parentDeclaration != null) reject("Consumer function must be top-level")
        if (Modifier.SUSPEND !in function.modifiers) reject("Consumer function must be suspending")
        if (function.typeParameters.isNotEmpty()) reject("Consumer function must not be generic")
        if (Modifier.PRIVATE in function.modifiers || Modifier.PROTECTED in function.modifiers) {
            reject("Consumer function must be public or internal")
        }
        if (function.extensionReceiver != null) reject("Consumer function must not have an extension receiver")
        if (function.parameters.isEmpty()) reject("Consumer function requires a message parameter")
        if (!valid) return null

        val message = concreteType(function.parameters.first(), "message") ?: return null
        if (message.name == CONSUME_CONTEXT) {
            reject("Consumer function must bind the message directly as its first parameter")
            return null
        }

        var contextCount = 0
        val arguments = function.parameters.mapIndexedNotNull { index, parameter ->
            if (index == 0) return@mapIndexedNotNull Argument.MESSAGE
            val type = concreteType(parameter, "dependency") ?: return@mapIndexedNotNull null
            if (type.name == CONSUME_CONTEXT) {
                contextCount++
                val contextMessage = type.arguments.singleOrNull()
                if (contextMessage != message.name) {
                    reject("ConsumeContext message type must match ${message.name}")
                }
                Argument.CONTEXT
            } else {
                Argument.Service(type.name)
            }
        }
        if (arguments.size != function.parameters.size) valid = false
        if (contextCount > 1) reject("Consumer function must not declare more than one ConsumeContext")

        val returnType = function.returnType?.resolve()
        val responseType = when {
            returnType == null -> {
                reject("Consumer function return type could not be resolved")
                null
            }
            returnType.declaration.qualifiedName?.asString() == "kotlin.Unit" -> null
            returnType.nullability == Nullability.NULLABLE -> {
                reject("Consumer function response must not be nullable")
                null
            }
            returnType.arguments.isNotEmpty() -> {
                reject("Consumer function response must be a concrete, non-generic class")
                null
            }
            returnType.declaration !is KSClassDeclaration -> {
                reject("Consumer function response must be a concrete class")
                null
            }
            else -> returnType.declaration.qualifiedName?.asString()
        }
        if (!valid) return null

        val qualifiedName = function.qualifiedName?.asString()
        if (qualifiedName == null) {
            reject("Consumer function must have a qualified name")
            return null
        }
        val annotation = function.annotations.single {
            it.annotationType.resolve().declaration.qualifiedName?.asString() == CONSUMER_FUNCTION
        }
        val configuredEndpoint = annotation.arguments
            .singleOrNull { it.name?.asString() == "endpointName" }
            ?.value as? String ?: ""
        val endpointName = configuredEndpoint.ifBlank { function.simpleName.asString() }
        val identity = "$qualifiedName(${function.parameters.joinToString(",") { it.type.resolve().declaration.qualifiedName?.asString().orEmpty() }})"

        return ConsumerFunctionModel(
            qualifiedName,
            identity,
            message.name,
            endpointName,
            configuredEndpoint.isNotBlank(),
            responseType,
            arguments,
            function.containingFile,
        )
    }

    private fun concreteType(parameter: KSValueParameter, role: String): ConcreteType? {
        val type = parameter.type.resolve()
        val declaration = type.declaration as? KSClassDeclaration
        val name = declaration?.qualifiedName?.asString()
        if (declaration == null || name == null || type.nullability == Nullability.NULLABLE) {
            logger.error("Consumer function $role parameter must be a concrete, non-null class", parameter)
            return null
        }
        if (name != CONSUME_CONTEXT && type.arguments.isNotEmpty()) {
            logger.error("Consumer function $role parameter must not be generic", parameter)
            return null
        }
        return ConcreteType(
            name,
            type.arguments.mapNotNull { it.type?.resolve()?.declaration?.qualifiedName?.asString() },
        )
    }

    private fun generate(functions: List<ConsumerFunctionModel>, classes: List<ConsumerClassModel>) {
        val dependencies = Dependencies(
            aggregating = true,
            *(functions.mapNotNull(ConsumerFunctionModel::origin) + classes.mapNotNull(ConsumerClassModel::origin))
                .distinct()
                .toTypedArray(),
        )
        codeGenerator.createNewFile(dependencies, generatedPackage, generatedName).bufferedWriter().use { writer ->
            writer.appendLine("package $generatedPackage")
            writer.appendLine()
            writer.appendLine("import com.myservicebus.kotlin.ConsumerCatalog")
            writer.appendLine("import com.myservicebus.kotlin.ConsumerFunctionInvoker")
            writer.appendLine("import com.myservicebus.kotlin.ServiceBusConfigurator")
            writer.appendLine()
            writer.appendLine("public object $generatedName : ConsumerCatalog {")
            writer.appendLine("    override fun register(configurator: ServiceBusConfigurator) {")
            classes.forEach { consumer -> writeClassRegistration(writer, consumer) }
            functions.forEach { function -> writeRegistration(writer, function) }
            writer.appendLine("    }")
            writer.appendLine("}")
        }
    }

    private fun writeClassRegistration(writer: Appendable, consumer: ConsumerClassModel) {
        val operation = if (consumer.handler) "registerHandlerClass" else "registerConsumerClass"
        writer.appendLine("        configurator.$operation(")
        val typeParameter = if (consumer.handler) "handlerType" else "consumerType"
        writer.appendLine("            $typeParameter = ${consumer.qualifiedName}::class.java,")
        val messageParameter = if (consumer.handler) "requestType" else "messageType"
        writer.appendLine("            $messageParameter = ${consumer.messageType}::class.java,")
        consumer.endpointName?.let { writer.appendLine("            endpointName = ${it.quoted()},") }
        writer.appendLine("            endpointNameExplicit = ${consumer.endpointNameExplicit},")
        writer.appendLine("        )")
    }

    private fun writeRegistration(writer: Appendable, function: ConsumerFunctionModel) {
        writer.appendLine("        configurator.registerConsumerFunction(")
        writer.appendLine("            functionIdentity = ${function.identity.quoted()},")
        writer.appendLine("            declarationType = $generatedName::class.java,")
        writer.appendLine("            messageType = ${function.messageType}::class.java,")
        writer.appendLine("            endpointName = ${function.endpointName.quoted()},")
        writer.appendLine("            endpointNameExplicit = ${function.endpointNameExplicit},")
        writer.appendLine("            responseType = ${function.responseType?.let { "$it::class.java" } ?: "null"},")
        writer.appendLine("            invoker = ConsumerFunctionInvoker { message, context, services ->")
        val arguments = function.arguments.joinToString(", ") {
            when (it) {
                Argument.MESSAGE -> "message"
                Argument.CONTEXT -> "context"
                is Argument.Service -> "services.getRequiredService(${it.type}::class.java)"
            }
        }
        writer.appendLine("                ${function.qualifiedName}($arguments)")
        writer.appendLine("            },")
        writer.appendLine("        )")
    }

    private fun String.quoted(): String = buildString {
        append('"')
        this@quoted.forEach { character ->
            when (character) {
                '\\' -> append("\\\\")
                '"' -> append("\\\"")
                '\n' -> append("\\n")
                '\r' -> append("\\r")
                '\t' -> append("\\t")
                else -> append(character)
            }
        }
        append('"')
    }

    private fun String.isKotlinIdentifier(): Boolean =
        isNotEmpty() && first().let { it == '_' || it.isLetter() } &&
            drop(1).all { it == '_' || it.isLetterOrDigit() }

    companion object {
        private const val CONSUMER_FUNCTION = "com.myservicebus.kotlin.ConsumerFunction"
        private const val CONSUME_CONTEXT = "com.myservicebus.kotlin.ConsumeContext"
        private const val CONSUMER = "com.myservicebus.kotlin.Consumer"
        private const val HANDLER = "com.myservicebus.kotlin.Handler"
        private const val MESSAGE_CONSUMER = "com.myservicebus.MessageConsumer"
    }
}

private data class ConcreteType(val name: String, val arguments: List<String>)

private sealed interface Argument {
    data object MESSAGE : Argument
    data object CONTEXT : Argument
    data class Service(val type: String) : Argument
}

private data class ConsumerFunctionModel(
    val qualifiedName: String,
    val identity: String,
    val messageType: String,
    val endpointName: String,
    val endpointNameExplicit: Boolean,
    val responseType: String?,
    val arguments: List<Argument>,
    val origin: com.google.devtools.ksp.symbol.KSFile?,
)

private data class ConsumerClassModel(
    val qualifiedName: String,
    val messageType: String,
    val endpointName: String?,
    val endpointNameExplicit: Boolean,
    val handler: Boolean,
    val origin: com.google.devtools.ksp.symbol.KSFile?,
)
