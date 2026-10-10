-- Season zip folder names changed (stable game ordering, de-duplicated same-name arrangements).
-- Drop cached zip rows so every zip regenerates with the new layout on its next download (takes
-- effect immediately). Regeneration overwrites the same {zip_key}.zip object; any zip never requested
-- again is left without a row and removed by SeasonZipCleanupService's orphan sweep once older than
-- OrphanGraceMinutes.
DELETE FROM season_share_zips;
