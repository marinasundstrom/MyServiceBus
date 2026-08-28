package com.myservicebus.di;

import com.google.inject.ScopeAnnotation;

import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;

@ScopeAnnotation
@Retention(RetentionPolicy.RUNTIME)
@interface Scoped {
}
