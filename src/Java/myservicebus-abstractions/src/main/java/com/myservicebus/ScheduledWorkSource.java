package com.myservicebus;

import java.util.List;
import java.util.concurrent.CompletionStage;

public interface ScheduledWorkSource {
    String getProvider();

    boolean isAuthoritative();

    CompletionStage<List<ScheduledWorkState>> getSnapshot(int maximumCount);
}
