package TestApp;

public final class OutboxShowcaseMessage {
    private String eventId;
    private String origin;
    private String createdAtUtc;

    public OutboxShowcaseMessage() {
    }

    public OutboxShowcaseMessage(String eventId, String origin, String createdAtUtc) {
        this.eventId = eventId;
        this.origin = origin;
        this.createdAtUtc = createdAtUtc;
    }

    public String getEventId() {
        return eventId;
    }

    public void setEventId(String eventId) {
        this.eventId = eventId;
    }

    public String getOrigin() {
        return origin;
    }

    public void setOrigin(String origin) {
        this.origin = origin;
    }

    public String getCreatedAtUtc() {
        return createdAtUtc;
    }

    public void setCreatedAtUtc(String createdAtUtc) {
        this.createdAtUtc = createdAtUtc;
    }
}
