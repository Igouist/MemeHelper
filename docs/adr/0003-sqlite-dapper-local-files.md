---
status: accepted
date: 2026-10-25
---

# Metadata 用 SQLite 加 Dapper，圖片放本機資料夾，file_id 快取

規模是數百到數千張圖、單一使用者、宿主在本機（ADR 0001）。Metadata 存 SQLite，用 `Microsoft.Data.Sqlite` 加 Dapper 手寫 SQL，schema 以編號 SQL 檔內嵌在組件裡、由 DbUp 在啟動時套用。圖片原檔放設定指定的資料夾，檔名為 `<編號>.<副檔名>`。第一次送圖後把 Telegram 回傳的 `file_id` 存進資料庫，之後重送直接用 ID，不再上傳檔案。

## 放棄的選項

- EF Core：作者偏好 Dapper。
- 手寫 `schema_version` 表：比 DbUp 少一個套件，但要自己處理交易與已套用判斷。
- 單一 JSON 索引檔：程式最少，但要自己處理寫入原子性。
- 雲端資料庫（Table Storage、Cosmos DB、MongoDB Atlas）：宿主在本機後沒有理由多一個外部依賴。
- 只靠 Telegram 存圖、不留原檔：管理 API 要顯示圖片時得經 `getFile` 下載，而且 `file_id` 綁定單一 bot，換 bot 就全部失效。

## 後果

- 整個系統的狀態就是 `Data/` 資料夾，備份很單純。
- 關鍵字搜尋在資料庫做子字串比對（`LIKE`），數千筆不需要索引。
