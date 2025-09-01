package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;

/**
 * Base context shared by all pipeline components.
 */
public interface PipeContext {

    /**
     * Token that signals when the operation should be cancelled.
     */
    CancellationToken getCancellationToken();
}