ALTER TABLE arrangement_files
    ADD COLUMN score_part_type TEXT,
    ADD COLUMN instrument_id INTEGER REFERENCES instruments(id) ON DELETE SET NULL;

-- Migrate existing PDF/ZIP files to unlisted_part
UPDATE arrangement_files
SET score_part_type = 'unlisted_part'
WHERE file_name ILIKE '%.pdf' OR file_name ILIKE '%.zip';
