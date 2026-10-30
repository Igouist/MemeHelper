# language: zh-TW
功能: 查看服務狀態
  身為唯一的使用者
  我想用一個網址看服務活著沒有
  以便不用翻 log 就知道 bot 還在收訊息、備份有在跑

  場景: 回報梗圖數量
    假設 梗圖庫有兩張梗圖，其中一張停用
    當 我取得 /api/status
    那麼 memeCount 為 2，enabledCount 為 1

  場景: 啟動後還沒成功 polling 過
    當 我取得 /api/status
    那麼 lastPollingSuccessAt 為 null

  場景: 回報最後一次 polling 成功的時間
    假設 bot 剛成功向 Telegram 取得一次更新
    當 我取得 /api/status
    那麼 lastPollingSuccessAt 是那一次的時間

  場景: 回報最後一次備份的時間
    假設 備份資料夾裡最新的快照是 2026-10-29 03:00 UTC 做的
    當 我取得 /api/status
    那麼 lastBackupAt 為 2026-10-29T03:00:00Z

  場景: 沒有設定備份
    假設 沒有設定 Backup:Directory
    當 我取得 /api/status
    那麼 lastBackupAt 為 null
