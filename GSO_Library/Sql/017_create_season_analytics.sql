-- Tracks aggregate page-access and file-download stats for a season's public share link.
CREATE TABLE season_analytics (
    season_id INTEGER PRIMARY KEY REFERENCES seasons(id) ON DELETE CASCADE,
    page_access_count INTEGER NOT NULL DEFAULT 0,
    page_last_accessed_at TIMESTAMPTZ,
    file_download_count INTEGER NOT NULL DEFAULT 0,
    file_last_downloaded_at TIMESTAMPTZ
);

INSERT INTO season_analytics (season_id)
SELECT id FROM seasons;
