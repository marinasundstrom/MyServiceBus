package com.myservicebus.rabbitmq;

public class MessageConfigurator<T> {
    Class<T> clz;

    public MessageConfigurator(Class<T> clz) {
        this.clz = clz;
    }

    public void setEntityName(String string) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'setEntityName'");
    }

}
