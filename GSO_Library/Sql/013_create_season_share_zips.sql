CREATE TABLE season_share_zips (
    id               SERIAL PRIMARY KEY,
    season_id        INTEGER NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    zip_key          TEXT NOT NULL,
    folder_path      TEXT NOT NULL,
    stored_file_name TEXT NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL,
    UNIQUE (season_id, zip_key)
);
