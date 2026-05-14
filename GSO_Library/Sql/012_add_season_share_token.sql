ALTER TABLE seasons
  ADD COLUMN share_token TEXT UNIQUE,
  ADD COLUMN share_include_pdf BOOLEAN NOT NULL DEFAULT TRUE,
  ADD COLUMN share_include_notation BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN share_include_playback BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN share_password_hash TEXT;
