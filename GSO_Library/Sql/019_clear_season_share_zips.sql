-- Season zip folder names changed (stable game ordering, de-duplicated same-name arrangements).
-- Drop cached zip metadata so every zip is regenerated with the new layout on next download;
-- regeneration overwrites the same {zip_key}.zip file in storage, so nothing is orphaned.
DELETE FROM season_share_zips;
