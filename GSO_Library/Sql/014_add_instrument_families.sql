CREATE TABLE IF NOT EXISTS instrument_families (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

INSERT INTO instrument_families (name) VALUES
    ('Woodwind'),
    ('Brass'),
    ('Percussion'),
    ('Keyboard'),
    ('Strings (Orchestral)'),
    ('Strings (Plucked)'),
    ('Voice'),
    ('Other');

ALTER TABLE instruments ADD COLUMN IF NOT EXISTS family_id INTEGER REFERENCES instrument_families(id) ON DELETE SET NULL;
