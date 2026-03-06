-- 005: Convert composer/arranger from single columns to multi-value tables

CREATE TABLE arrangement_composers (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (arrangement_id, name)
);

CREATE TABLE arrangement_arrangers (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (arrangement_id, name)
);

CREATE INDEX idx_arrangement_composers_name ON arrangement_composers(name);
CREATE INDEX idx_arrangement_arrangers_name ON arrangement_arrangers(name);

-- Migrate existing data
INSERT INTO arrangement_composers (arrangement_id, name, sort_order)
SELECT id, composer, 0 FROM arrangements WHERE composer IS NOT NULL AND composer != '';

INSERT INTO arrangement_arrangers (arrangement_id, name, sort_order)
SELECT id, arranger, 0 FROM arrangements WHERE arranger IS NOT NULL AND arranger != '';

-- Drop old columns
ALTER TABLE arrangements DROP COLUMN composer;
ALTER TABLE arrangements DROP COLUMN arranger;
