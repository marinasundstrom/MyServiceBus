package com.myservicebus;

import java.lang.annotation.*;

/** Wire identity, independent of the Java type name and broker entity name. */
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.TYPE)
public @interface MessageUrnName {
    String value();
    boolean useDefaultPrefix() default true;
}
