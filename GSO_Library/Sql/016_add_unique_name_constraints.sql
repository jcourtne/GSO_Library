-- Backs the application-level duplicate-name checks with real unique constraints
-- so concurrent requests can no longer race past the check-then-insert and create duplicates.
CREATE UNIQUE INDEX IF NOT EXISTS ux_games_name_lower ON games (LOWER(name));
CREATE UNIQUE INDEX IF NOT EXISTS ux_series_name_lower ON series (LOWER(name));
CREATE UNIQUE INDEX IF NOT EXISTS ux_instruments_name_lower ON instruments (LOWER(name));
