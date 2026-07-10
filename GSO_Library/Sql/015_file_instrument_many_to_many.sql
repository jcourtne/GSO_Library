CREATE TABLE arrangement_file_instruments (
    file_id       INTEGER NOT NULL REFERENCES arrangement_files(id) ON DELETE CASCADE,
    instrument_id INTEGER NOT NULL REFERENCES instruments(id) ON DELETE CASCADE,
    PRIMARY KEY (file_id, instrument_id)
);

-- Migrate existing single-instrument assignments
INSERT INTO arrangement_file_instruments (file_id, instrument_id)
SELECT id, instrument_id FROM arrangement_files WHERE instrument_id IS NOT NULL;

ALTER TABLE arrangement_files DROP COLUMN instrument_id;
