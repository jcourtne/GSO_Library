CREATE TABLE IF NOT EXISTS audit_events (
    id              SERIAL PRIMARY KEY,
    event_type      TEXT        NOT NULL,
    username        TEXT,
    target_username TEXT,
    ip_address      TEXT,
    detail          TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_audit_events_event_type ON audit_events(event_type);
CREATE INDEX IF NOT EXISTS ix_audit_events_username   ON audit_events(username);
CREATE INDEX IF NOT EXISTS ix_audit_events_created_at ON audit_events(created_at);
