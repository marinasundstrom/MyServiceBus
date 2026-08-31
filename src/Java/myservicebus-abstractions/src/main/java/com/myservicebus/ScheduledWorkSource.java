package com.myservicebus;

import java.util.List;

public interface ScheduledWorkSource {
    String getProvider();

    boolean isAuthoritative();

    List<ScheduledWorkState> getSnapshot();
}
