package com.myservicebus;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Declares a consumer method or overrides the receive endpoint mapping of a consumer type.
 * A non-blank value is an explicit receive endpoint name. A bare method annotation
 * derives the endpoint from the method name.
 */
@Retention(RetentionPolicy.RUNTIME)
@Target({ ElementType.TYPE, ElementType.METHOD })
public @interface MessageConsumer {
    String value() default "";
}
