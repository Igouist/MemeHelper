CREATE TABLE memes (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    extension        TEXT    NOT NULL,
    is_enabled       INTEGER NOT NULL DEFAULT 1,
    telegram_file_id TEXT,
    created_at       TEXT    NOT NULL,
    updated_at       TEXT    NOT NULL
);

CREATE TABLE meme_keywords (
    meme_id INTEGER NOT NULL REFERENCES memes(id) ON DELETE CASCADE,
    keyword TEXT    NOT NULL,
    PRIMARY KEY (meme_id, keyword)
);
