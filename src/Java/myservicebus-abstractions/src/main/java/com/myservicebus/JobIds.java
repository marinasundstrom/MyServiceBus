package com.myservicebus;

import java.util.UUID;

final class JobIds {
    private JobIds() {
    }

    static boolean isEmpty(UUID value) {
        return value != null && value.getMostSignificantBits() == 0 && value.getLeastSignificantBits() == 0;
    }
}

