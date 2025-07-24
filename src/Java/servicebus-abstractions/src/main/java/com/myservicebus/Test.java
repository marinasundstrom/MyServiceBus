package com.myservicebus;

import java.net.URI;
import java.util.HashMap;
import java.util.Map;

import com.myservicebus.contexts.ReceiveContext;
import com.myservicebus.contexts.ReceiveContextImpl;
import com.myservicebus.contexts.SendContextBase;
import com.myservicebus.contexts.SendContextImpl;
import com.myservicebus.middleware.LoggingFilter;
import com.myservicebus.middleware.PipeBuilder;

public class Test {
    public void test() throws Exception {
        var builder = new PipeBuilder<SendContextBase>();
        builder.use(new LoggingFilter<SendContextBase>());
        // builder.use(nxwew ValidationFilter<SendContextBase>()); // your own

        var pipe = builder.build();

        pipe.send(new SendContextImpl<String>(null, new URI("queue:orders")));
    }

    public void test2() throws Exception {
        var builder = new PipeBuilder<ReceiveContext>();
        // builder.use(new JsonDeserializerFilter(new YourTypeResolver()));
        builder.use(new LoggingFilter<ReceiveContext>());
        var receivePipe = builder.build();

        byte[] rawBytes = {};
        Map<String, Object> headerDict = new HashMap<>();

        // Inside your RabbitMQ transport:
        var receiveContext = new ReceiveContextImpl(rawBytes, "application/json", headerDict,
                "MyApp.Messages.UserCreated", null);

        receivePipe.send(receiveContext);
    }
}
