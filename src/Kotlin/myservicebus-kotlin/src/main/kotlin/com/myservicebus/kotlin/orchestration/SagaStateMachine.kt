package com.myservicebus.kotlin.orchestration

import com.myservicebus.BusRegistrationConfigurator
import com.myservicebus.MessageUrn
import com.myservicebus.di.ServiceProvider
import com.myservicebus.orchestration.InMemorySagaRepository
import com.myservicebus.orchestration.SagaCompletionPolicy
import com.myservicebus.orchestration.SagaConcurrencyKind
import com.myservicebus.orchestration.SagaCorrelationKind
import com.myservicebus.orchestration.SagaCreationPolicy
import com.myservicebus.orchestration.SagaDurabilityKind
import com.myservicebus.orchestration.SagaMissingInstancePolicy
import com.myservicebus.orchestration.SagaOutboxKind
import com.myservicebus.orchestration.SagaRepository
import com.myservicebus.orchestration.SagaRepositoryRequirements
import com.myservicebus.orchestration.SagaStateMachineDefinition
import com.myservicebus.orchestration.SagaStateMachineDefinitionBuilder
import com.myservicebus.orchestration.SagaStateMachineConsumerRegistration
import com.myservicebus.orchestration.SagaStateMachineRuntime
import com.myservicebus.orchestration.SagaStateMachineRuntimeBuilder
import com.myservicebus.orchestration.SagaStateMachineRegistration
import java.util.UUID
import java.util.concurrent.CompletableFuture
import kotlin.coroutines.CoroutineContext
import kotlin.properties.ReadOnlyProperty
import kotlin.reflect.KMutableProperty1
import kotlin.reflect.KProperty
import kotlin.reflect.KProperty1
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

/**
 * Defines a saga state machine with Kotlin properties, receivers, reified message types, and
 * suspending activities while using the shared JVM saga definition and runtime.
 */
abstract class SagaStateMachine<TSaga : Any>(
    private val id: String,
    private val version: String,
    private val owner: String,
    private val sagaDataUrn: String,
    private val coroutineContext: CoroutineContext = Dispatchers.Default,
) {
    private val states = mutableListOf<State>()
    private val events = mutableListOf<EventRegistration<*>>()
    private val behaviors = mutableListOf<BehaviorRegistration<*>>()
    private var stateMember: String? = null
    private var getState: ((TSaga) -> String?)? = null
    private var setState: ((TSaga, String) -> Unit)? = null
    private var instanceFactory: ((UUID) -> TSaga)? = null
    private var cloneInstance: ((TSaga) -> TSaga)? = null
    private var completionPolicy = SagaCompletionPolicy.RETAIN
    private var requirements = SagaRepositoryRequirements(
        SagaCorrelationKind.IDENTITY,
        SagaConcurrencyKind.SINGLE_PROCESS,
        SagaDurabilityKind.VOLATILE,
        SagaOutboxKind.LOGICAL,
    )
    private var builtDefinition: SagaStateMachineDefinition? = null
    private var frozen = false

    /** The normalized, language-independent description of this state machine. */
    fun definition(): SagaStateMachineDefinition {
        builtDefinition?.let { return it }
        freezeAndValidate()
        val builder = SagaStateMachineDefinitionBuilder(
            id,
            version,
            owner,
            sagaDataUrn,
            requireNotNull(stateMember),
        )
        states.forEach { builder.state(it.id) }
        events.forEach { it.describe(builder) }
        behaviors.forEach { it.describe(builder) }
        builder.requires(requirements)
        if (completionPolicy == SagaCompletionPolicy.DELETE_WHEN_FINALIZED) {
            builder.deleteWhenFinalized()
        }
        return builder.build().also { builtDefinition = it }
    }

    /** Creates an executable state machine over a shared JVM saga repository. */
    fun createRuntime(repository: SagaRepository<TSaga>): SagaStateMachineRuntime<TSaga> {
        val builder = SagaStateMachineRuntimeBuilder(
            definition(),
            repository,
            requireNotNull(instanceFactory),
            requireNotNull(getState),
            requireNotNull(setState),
        )
        events.forEach { it.bind(builder) }
        behaviors.forEach { it.bind(builder) }
        return builder.build()
    }

    /** Creates the process-local repository intended for development and tests. */
    fun createInMemoryRepository(): InMemorySagaRepository<TSaga> {
        freezeAndValidate()
        return InMemorySagaRepository(requireNotNull(cloneInstance))
    }

    /** Creates the shared registration consumed by the Kotlin service-bus configurator. */
    fun registration(
        repositoryCapabilities: com.myservicebus.orchestration.SagaRepositoryCapabilities,
        repositoryFactory: (ServiceProvider) -> SagaRepository<TSaga>,
    ): SagaStateMachineRegistration<TSaga> {
        val registration = SagaStateMachineRegistration(
            this,
            this::class.java,
            definition(),
            repositoryCapabilities,
            repositoryFactory,
            ::createRuntime,
        )
        events.forEach { it.addTo(registration) }
        return registration
    }

    /** Registers every declared event as an ordinary bus consumer on one endpoint. */
    fun registerConsumers(
        configurator: BusRegistrationConfigurator,
        endpointName: String,
        runtimeFactory: (ServiceProvider) -> SagaStateMachineRuntime<TSaga>,
    ) {
        val definition = definition()
        events.forEach {
            it.register(
                configurator,
                runtimeFactory,
                this::class.java,
                endpointName,
                definition,
            )
        }
    }

    protected fun <TState : String?> instanceState(property: KMutableProperty1<TSaga, TState>) {
        ensureMutable()
        stateMember = property.name.toDefinitionName()
        getState = property::get
        setState = { saga, state ->
            @Suppress("UNCHECKED_CAST")
            property.set(saga, state as TState)
        }
    }

    protected fun instanceFactory(factory: (UUID) -> TSaga) {
        ensureMutable()
        instanceFactory = factory
    }

    protected fun cloneInstance(clone: (TSaga) -> TSaga) {
        ensureMutable()
        cloneInstance = clone
    }

    protected fun state(): StateDelegate {
        ensureMutable()
        return StateDelegate()
    }

    protected inline fun <reified TMessage : Any> event(
        noinline configure: EventCorrelation<TSaga, TMessage>.() -> Unit,
    ): EventDelegate<TMessage> = event(TMessage::class.java, configure)

    protected inline fun <reified TMessage : Any> event(
        messageUrn: String,
        noinline configure: EventCorrelation<TSaga, TMessage>.() -> Unit,
    ): EventDelegate<TMessage> = event(messageUrn, TMessage::class.java, configure)

    protected fun <TMessage : Any> event(
        messageType: Class<TMessage>,
        configure: EventCorrelation<TSaga, TMessage>.() -> Unit,
    ): EventDelegate<TMessage> = event(MessageUrn.forClass(messageType), messageType, configure)

    protected fun <TMessage : Any> event(
        messageUrn: String,
        messageType: Class<TMessage>,
        configure: EventCorrelation<TSaga, TMessage>.() -> Unit,
    ): EventDelegate<TMessage> {
        ensureMutable()
        return EventDelegate(messageUrn, messageType, configure)
    }

    protected fun initially(configure: BehaviorScope.() -> Unit) {
        behavior(SagaStateMachineDefinition.INITIAL_STATE, configure)
    }

    protected fun during(state: State, configure: BehaviorScope.() -> Unit) {
        behavior(state.id, configure)
    }

    protected fun duringAny(configure: BehaviorScope.() -> Unit) {
        behavior(SagaStateMachineDefinition.ANY_STATE, configure)
    }

    protected fun deleteWhenFinalized() {
        ensureMutable()
        completionPolicy = SagaCompletionPolicy.DELETE_WHEN_FINALIZED
    }

    protected fun retainWhenFinalized() {
        ensureMutable()
        completionPolicy = SagaCompletionPolicy.RETAIN
    }

    protected fun repositoryRequirements(value: SagaRepositoryRequirements) {
        ensureMutable()
        requirements = value
    }

    inner class StateDelegate internal constructor() : ReadOnlyProperty<SagaStateMachine<TSaga>, State> {
        private lateinit var state: State

        operator fun provideDelegate(
            thisRef: SagaStateMachine<TSaga>,
            property: KProperty<*>,
        ): StateDelegate {
            val declared = State(property.name.toDefinitionName())
            require(states.none { it.id == declared.id }) { "Saga state '${declared.id}' is already declared." }
            states += declared
            state = declared
            return this
        }

        override fun getValue(thisRef: SagaStateMachine<TSaga>, property: KProperty<*>): State = state
    }

    inner class EventDelegate<TMessage : Any> internal constructor(
        private val messageUrn: String,
        private val messageType: Class<TMessage>,
        private val configure: EventCorrelation<TSaga, TMessage>.() -> Unit,
    ) : ReadOnlyProperty<SagaStateMachine<TSaga>, Event<TMessage>> {
        private lateinit var event: Event<TMessage>

        operator fun provideDelegate(
            thisRef: SagaStateMachine<TSaga>,
            property: KProperty<*>,
        ): EventDelegate<TMessage> {
            val declared = Event(property.name.toDefinitionName(), messageUrn, messageType)
            require(events.none { it.event.id == declared.id }) { "Saga event '${declared.id}' is already declared." }
            val correlation = EventCorrelation<TSaga, TMessage>().apply(configure)
            correlation.validate(declared.id)
            events += EventRegistration(declared, correlation)
            event = declared
            return this
        }

        override fun getValue(thisRef: SagaStateMachine<TSaga>, property: KProperty<*>): Event<TMessage> = event
    }

    protected inner class BehaviorScope internal constructor(private val sourceState: String) {
        fun <TMessage : Any> on(event: Event<TMessage>, configure: ActivityBinder<TMessage>.() -> Unit) {
            val binder = ActivityBinder(event).apply(configure)
            val activities = binder.registeredActivities()
            require(activities.isNotEmpty()) {
                "Saga behavior '$sourceState/${event.id}' must declare at least one activity."
            }
            require(behaviors.none { it.sourceState == sourceState && it.event.id == event.id }) {
                "Saga behavior '$sourceState/${event.id}' is already declared."
            }
            behaviors += BehaviorRegistration(sourceState, event, activities)
        }

        fun <TMessage : Any> ignore(event: Event<TMessage>) {
            on(event) { ignore() }
        }
    }

    protected inner class ActivityBinder<TMessage : Any> internal constructor(
        private val event: Event<TMessage>,
    ) {
        private val activities = mutableListOf<ActivityRegistration<TMessage>>()

        internal fun registeredActivities(): List<ActivityRegistration<TMessage>> = activities.toList()

        fun then(execute: suspend SagaActivityContext<TSaga, TMessage>.() -> Unit) {
            add(MutateActivity(execute))
        }

        inline fun <reified TOutgoing : Any> send(
            destination: String,
            noinline create: suspend SagaActivityContext<TSaga, TMessage>.() -> TOutgoing,
        ) {
            send(MessageUrn.forClass(TOutgoing::class.java), destination, create)
        }

        fun <TOutgoing : Any> send(
            messageUrn: String,
            destination: String,
            create: suspend SagaActivityContext<TSaga, TMessage>.() -> TOutgoing,
        ) {
            add(MessageActivity(messageUrn, destination, create))
        }

        inline fun <reified TOutgoing : Any> publish(
            noinline create: suspend SagaActivityContext<TSaga, TMessage>.() -> TOutgoing,
        ) {
            publish(MessageUrn.forClass(TOutgoing::class.java), create)
        }

        fun <TOutgoing : Any> publish(
            messageUrn: String,
            create: suspend SagaActivityContext<TSaga, TMessage>.() -> TOutgoing,
        ) {
            add(MessageActivity(messageUrn, null, create))
        }

        fun transitionTo(state: State) {
            add(TransitionActivity(state.id))
        }

        fun finalizeSaga() {
            add(FinalizeActivity())
        }

        fun ignore() {
            add(IgnoreActivity())
        }

        private fun add(activity: ActivityRegistration<TMessage>) {
            check(activities.none { it.terminal }) { "No activity can follow transition, finalize, or ignore." }
            activities += activity
        }
    }

    private fun behavior(sourceState: String, configure: BehaviorScope.() -> Unit) {
        ensureMutable()
        BehaviorScope(sourceState).configure()
    }

    private fun freezeAndValidate() {
        frozen = true
        checkNotNull(stateMember) { "The saga state property must be configured." }
        checkNotNull(getState) { "The saga state getter must be configured." }
        checkNotNull(setState) { "The saga state setter must be configured." }
        checkNotNull(instanceFactory) { "The saga instance factory must be configured." }
        checkNotNull(cloneInstance) { "The saga clone function must be configured." }
    }

    private fun ensureMutable() {
        check(!frozen) { "The saga state machine is frozen and cannot be changed." }
    }

    private inner class EventRegistration<TMessage : Any>(
        val event: Event<TMessage>,
        private val correlation: EventCorrelation<TSaga, TMessage>,
    ) {
        fun describe(builder: SagaStateMachineDefinitionBuilder) {
            builder.event(event.id, event.messageUrn) { configured ->
                configured.correlateById(
                    requireNotNull(correlation.sagaMember),
                    requireNotNull(correlation.messageMember),
                )
                if (correlation.creationPolicy == SagaCreationPolicy.IF_MISSING) configured.createsIfMissing()
                if (correlation.missingPolicy == SagaMissingInstancePolicy.DISCARD) configured.discardIfMissing()
            }
        }

        fun bind(builder: SagaStateMachineRuntimeBuilder<TSaga>) {
            builder.event(event.id, event.messageType, requireNotNull(correlation.correlate))
        }

        fun register(
            configurator: BusRegistrationConfigurator,
            runtimeFactory: (ServiceProvider) -> SagaStateMachineRuntime<TSaga>,
            stateMachineClass: Class<*>,
            endpointName: String,
            definition: SagaStateMachineDefinition,
        ) {
            SagaStateMachineConsumerRegistration.register(
                configurator,
                runtimeFactory,
                stateMachineClass,
                endpointName,
                definition,
                event.id,
                event.messageType,
                requireNotNull(correlation.correlate),
            )
        }

        fun addTo(registration: SagaStateMachineRegistration<TSaga>) {
            registration.event(event.id, event.messageType, requireNotNull(correlation.correlate))
        }
    }

    private inner class BehaviorRegistration<TMessage : Any>(
        val sourceState: String,
        val event: Event<TMessage>,
        private val activities: List<ActivityRegistration<TMessage>>,
    ) {
        fun describe(builder: SagaStateMachineDefinitionBuilder) {
            val configure = { behavior: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder ->
                activities.forEachIndexed { index, activity ->
                    activity.describe(behavior, "$sourceState.${event.id}.$index")
                }
            }
            when (sourceState) {
                SagaStateMachineDefinition.INITIAL_STATE -> builder.initially(event.id, configure)
                SagaStateMachineDefinition.ANY_STATE -> builder.duringAny(event.id, configure)
                else -> builder.during(sourceState, event.id, configure)
            }
        }

        fun bind(builder: SagaStateMachineRuntimeBuilder<TSaga>) {
            activities.forEachIndexed { index, activity ->
                activity.bind(builder, sourceState, event, index)
            }
        }
    }

    internal abstract inner class ActivityRegistration<TMessage : Any> {
        abstract val terminal: Boolean

        abstract fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        )

        abstract fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        )
    }

    private inner class MutateActivity<TMessage : Any>(
        private val execute: suspend SagaActivityContext<TSaga, TMessage>.() -> Unit,
    ) : ActivityRegistration<TMessage>() {
        override val terminal = false

        override fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        ) {
            builder.mutate(activityId)
        }

        override fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        ) {
            builder.mutate(sourceState, event.id, index, event.messageType) { context ->
                futureVoid { execute(SagaActivityContext(context.saga(), context.message(), context.correlationId())) }
            }
        }
    }

    private inner class MessageActivity<TMessage : Any, TOutgoing : Any>(
        private val messageUrn: String,
        private val destination: String?,
        private val create: suspend SagaActivityContext<TSaga, TMessage>.() -> TOutgoing,
    ) : ActivityRegistration<TMessage>() {
        override val terminal = false

        override fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        ) {
            if (destination == null) builder.publish(messageUrn) else builder.send(messageUrn, destination)
        }

        override fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        ) {
            builder.message(sourceState, event.id, index, event.messageType) { context ->
                future { create(SagaActivityContext(context.saga(), context.message(), context.correlationId())) }
            }
        }
    }

    private inner class TransitionActivity<TMessage : Any>(private val targetState: String) :
        ActivityRegistration<TMessage>() {
        override val terminal = true

        override fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        ) {
            builder.transitionTo(targetState)
        }

        override fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        ) = Unit
    }

    private inner class FinalizeActivity<TMessage : Any> : ActivityRegistration<TMessage>() {
        override val terminal = true

        override fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        ) {
            builder.finalizeSaga()
        }

        override fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        ) = Unit
    }

    private inner class IgnoreActivity<TMessage : Any> : ActivityRegistration<TMessage>() {
        override val terminal = true

        override fun describe(
            builder: SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder,
            activityId: String,
        ) {
            builder.ignore()
        }

        override fun bind(
            builder: SagaStateMachineRuntimeBuilder<TSaga>,
            sourceState: String,
            event: Event<TMessage>,
            index: Int,
        ) = Unit
    }

    private fun <T> future(block: suspend () -> T): CompletableFuture<T> {
        val future = CompletableFuture<T>()
        val job: Job = CoroutineScope(coroutineContext).launch {
            try {
                future.complete(block())
            } catch (failure: Throwable) {
                future.completeExceptionally(failure)
            }
        }
        future.whenComplete { _, _ -> if (future.isCancelled) job.cancel() }
        return future
    }

    private fun futureVoid(block: suspend () -> Unit): CompletableFuture<Void> {
        val future = CompletableFuture<Void>()
        val job: Job = CoroutineScope(coroutineContext).launch {
            try {
                block()
                future.complete(null)
            } catch (failure: Throwable) {
                future.completeExceptionally(failure)
            }
        }
        future.whenComplete { _, _ -> if (future.isCancelled) job.cancel() }
        return future
    }
}

data class State(val id: String)

data class Event<TMessage : Any>(
    val id: String,
    val messageUrn: String,
    val messageType: Class<TMessage>,
)

class EventCorrelation<TSaga : Any, TMessage : Any> {
    internal var sagaMember: String? = null
    internal var messageMember: String? = null
    internal var correlate: ((TMessage) -> UUID)? = null
    internal var creationPolicy = SagaCreationPolicy.EXISTING_ONLY
    internal var missingPolicy = SagaMissingInstancePolicy.FAULT

    fun correlateById(
        sagaProperty: KProperty1<TSaga, UUID>,
        messageProperty: KProperty1<TMessage, UUID>,
    ) {
        sagaMember = sagaProperty.name.toDefinitionName()
        messageMember = messageProperty.name.toDefinitionName()
        correlate = messageProperty::get
    }

    fun createsIfMissing() {
        creationPolicy = SagaCreationPolicy.IF_MISSING
    }

    fun existingOnly() {
        creationPolicy = SagaCreationPolicy.EXISTING_ONLY
    }

    fun discardIfMissing() {
        missingPolicy = SagaMissingInstancePolicy.DISCARD
    }

    fun faultIfMissing() {
        missingPolicy = SagaMissingInstancePolicy.FAULT
    }

    internal fun validate(eventId: String) {
        check(sagaMember != null && messageMember != null && correlate != null) {
            "Saga event '$eventId' must declare identity correlation."
        }
    }
}

class SagaActivityContext<TSaga : Any, TMessage : Any> internal constructor(
    val saga: TSaga,
    val message: TMessage,
    val correlationId: UUID,
)

private fun String.toDefinitionName(): String = replaceFirstChar { it.uppercase() }
