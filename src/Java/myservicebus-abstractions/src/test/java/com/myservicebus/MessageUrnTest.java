package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;

import org.junit.jupiter.api.Test;

class MessageUrnTest {
    interface RootEventContract {
    }

    interface EventContract extends RootEventContract {
    }

    interface BaseEventContract {
    }

    static class BaseEvent implements BaseEventContract {
    }

    static class ConcreteEvent extends BaseEvent implements EventContract {
    }

    @Test
    void messageTypesIncludeConcreteBaseAndInterfaceContracts() {
        assertEquals(
                List.of(
                        MessageUrn.forClass(ConcreteEvent.class),
                        MessageUrn.forClass(BaseEvent.class),
                        MessageUrn.forClass(BaseEventContract.class),
                        MessageUrn.forClass(EventContract.class),
                        MessageUrn.forClass(RootEventContract.class)),
                MessageUrn.forMessageTypes(ConcreteEvent.class));
    }
}
