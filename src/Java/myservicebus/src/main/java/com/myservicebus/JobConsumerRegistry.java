package com.myservicebus;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Semaphore;

import com.myservicebus.di.ServiceProvider;

public final class JobConsumerRegistry {
    public static final class Descriptor {
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

        public Class<?> jobType() {
            return jobType;
        }

        public JobConsumerOptions options() {
            return options;
        }

        public String jobTypeName() {
            return jobTypeName;
        }

        public Semaphore concurrency() {
            return concurrency;
        }

        @SuppressWarnings("unchecked")
        public java.util.concurrent.CompletionStage<Void> run(ServiceProvider services, JobExecutionContext context)
                throws Exception {
            JobConsumer<Object> consumer = (JobConsumer<Object>) services.getRequiredService(consumerType);
            return consumer.run(new InMemoryJobContext<>(context, context.job()));
        }
    }

    private final Map<Class<?>, Descriptor> descriptors = new ConcurrentHashMap<>();
    private final Map<String, Descriptor> descriptorsByName = new ConcurrentHashMap<>();

    void add(Class<?> consumerType, Class<?> jobType, JobConsumerOptions options) {
        Descriptor descriptor = new Descriptor(consumerType, jobType, options);
        if (descriptors.containsKey(jobType)) {
            throw new IllegalStateException("A job consumer is already registered for " + jobType.getName());
        }
        if (descriptorsByName.containsKey(descriptor.jobTypeName())) {
            throw new IllegalStateException(
                    "A job consumer is already registered for job type name '" + descriptor.jobTypeName() + "'");
        }

        descriptors.put(jobType, descriptor);
        descriptorsByName.put(descriptor.jobTypeName(), descriptor);
    }

    public Descriptor get(Class<?> jobType) {
        Descriptor descriptor = descriptors.get(jobType);
        if (descriptor == null) {
            throw new IllegalStateException("No job consumer is registered for " + jobType.getName());
        }
        return descriptor;
    }

    public Descriptor get(String jobTypeName) {
        Descriptor descriptor = descriptorsByName.get(jobTypeName);
        if (descriptor == null) {
            throw new IllegalStateException("No job consumer is registered for job type name '" + jobTypeName + "'");
        }
        return descriptor;
    }
}
