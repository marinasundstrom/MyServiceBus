package com.myservicebus.persistence.postgresql;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.persistence.OutboxSession;
import java.util.Objects;
import java.util.function.Supplier;
import javax.sql.DataSource;

/** Registration helpers for durable PostgreSQL saga state machines. */
public final class PostgreSqlSagas {
    private PostgreSqlSagas() {
    }

    public static <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            BusRegistrationConfigurator configurator,
            Class<TStateMachine> stateMachineType,
            Supplier<TStateMachine> stateMachineFactory,
            Class<TSaga> sagaType,
            DataSource dataSource,
            String serviceName,
            String endpointName) {
        Objects.requireNonNull(configurator, "configurator");
        Objects.requireNonNull(stateMachineType, "stateMachineType");
        Objects.requireNonNull(stateMachineFactory, "stateMachineFactory");
        Objects.requireNonNull(sagaType, "sagaType");
        Objects.requireNonNull(dataSource, "dataSource");

        configurator.addSagaStateMachine(
                stateMachineType,
                stateMachineFactory,
                PostgreSqlSagaRepository.CAPABILITIES,
                provider -> new PostgreSqlSagaRepository<>(
                        dataSource,
                        provider.getRequiredService(OutboxSession.class),
                        serviceName,
                        sagaType),
                endpointName);
    }
}
