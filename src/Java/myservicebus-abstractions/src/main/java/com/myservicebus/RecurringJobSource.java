package com.myservicebus;

import java.util.List;
import java.util.concurrent.CompletionStage;

public interface RecurringJobSource {
    String getProvider();

    boolean isAuthoritative();

    CompletionStage<List<RecurringJobState>> getSnapshot(int maximumCount);
}
