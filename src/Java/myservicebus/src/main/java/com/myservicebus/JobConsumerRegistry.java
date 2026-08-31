package com.myservicebus;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Semaphore;

import com.myservicebus.di.ServiceProvider;

final class JobConsumerRegistry {
    static final class Descriptor {
        private final Class<?> consumerType;
        private final Class<?> jobType;
        private final JobConsumerOptions options;
        private final String jobTypeName;
        private final Semaphore concurrency;

        Descriptor(Class<?> consumerType, Class<?> jobType, JobConsumerOptions options) {
            this.consumerType = consumerType;
            this.jobType = jobType;
            this.options = options;
            jobTypeName = options.getJobTypeName() != null ? options.getJobTypeName() : jobType.getSimpleName();
            concurrency = new Semaphore(options.getConcurrentJobLimit());
        }

        Class<?> jobType() {
            return jobType;
        }

        JobConsumerOptions options() {
            return options;
        }

        String jobTypeName() {
            return jobTypeName;
        }

        Semaphore concurrency() {
            return concurrency;
        }

        @SuppressWarnings("unchecked")
        java.util.concurrent.CompletionStage<Void> run(ServiceProvider services, JobExecutionContext context)
                throws Exception {
            JobConsumer<Object> consumer = (JobConsumer<Object>) services.getRequiredService(consumerType);
            return consumer.run(new InMemoryJobContext<>(context, context.job()));
        }
    }

    private final Map<Class<?>, Descriptor> descriptors = new ConcurrentHashMap<>();

    void add(Class<?> consumerType, Class<?> jobType, JobConsumerOptions options) {
        Descriptor descriptor = new Descriptor(consumerType, jobType, options);
        if (descriptors.putIfAbsent(jobType, descriptor) != null) {
            throw new IllegalStateException("A job consumer is already registered for " + jobType.getName());
        }
    }

    Descriptor get(Class<?> jobType) {
        Descriptor descriptor = descriptors.get(jobType);
        if (descriptor == null) {
            throw new IllegalStateException("No job consumer is registered for " + jobType.getName());
        }
        return descriptor;
    }
}

