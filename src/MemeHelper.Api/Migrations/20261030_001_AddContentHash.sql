-- 上傳去重：內容的 SHA-256。升級前的資料留空，啟動時由程式從圖片檔補算；
-- SQLite 的唯一索引允許多個 NULL，所以舊資料不會互相衝突。
ALTER TABLE memes ADD COLUMN content_hash TEXT;

CREATE UNIQUE INDEX ux_memes_content_hash ON memes (content_hash);
