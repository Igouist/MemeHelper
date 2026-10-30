# 專案架構

一句話：**一個 ASP.NET Core 程序，兩個入口，共用一套核心。**

```
   Telegram 使用者                    瀏覽器 / 腳本
        |                                  |
  long polling                        HTTP (Swagger UI)
        |                                  |
  Telegram/                          Features/Memes/*  (一個 endpoint 一個檔案)
  TelegramPollingService                   |
  MemeBotHandler                           |
        \                                  /
         \___________ 共用核心 ___________/
                        |
     Features/Search  (關鍵字比對，只有這一份)
     Features/Memes   (Meme 模型、關鍵字正規化、圖片檔規則、IImageStore)
     Database/        (IMemeRepository：Dapper + SQLite)
                        |
                  Data/memehelper.db  +  Data/images/{編號}.{副檔名}
```

## 目錄

```
src/MemeHelper.Api/
  Program.cs            組裝 DI、跑 migration、掛 endpoint、啟動
  Configuration/        IOptions 類別（Telegram、Database、Storage、Search、Backup）
  Features/Memes/       Meme 模型、KeywordNormalizer、ImageFileRules、FileSystemImageStore
                        以及 UploadMeme、ListMemes、GetMeme、GetMemeImage、AddKeywords、
                        RemoveKeyword、DisableMeme、EnableMeme、DeleteMeme 九個 endpoint
  Features/Search/      KeywordMatcher（純函式）、MemeSearch
  Database/             SqliteConnectionFactory、DatabaseMigrator（DbUp）、SqliteMemeRepository
  Migrations/           編號 SQL 檔，啟動時套用，已執行過的不可修改
  Telegram/             TelegramPollingService、IncomingMessage（訊息分類，純函式）、
                        MemeBotHandler、ReplyFormatter、IBotMessenger 與其 Telegram.Bot 實作、
                        LongPollingBotClient
  Backup/               DataBackup（快照、複製新圖、清舊快照）、BackupService（每 24 小時排程）
tests/MemeHelper.Tests/ 對應上述結構；Support/ 放 ApiFactory、TempDatabase 等測試基礎
deploy/                 systemd unit 範本
docs/                   設計、場景（Gherkin）、ADR、這份指南
```

## 一次查詢的流程

使用者傳「六點 下班」：

1. `TelegramPollingService` 收到 `Update`，交給 `IncomingMessage.From` 分類：文字是查詢，照片與檔案是新增，其他忽略。文字就呼叫 `MemeBotHandler.HandleTextAsync`。
2. Handler 先看 user id 是否在白名單，不在就沉默。
3. 純數字先當編號查；否則 `MemeSearch.FindAsync`：整批讀出啟用中的梗圖，用 `KeywordMatcher` 過濾。規則是每個詞都要有任一關鍵字包含它（子字串，英文不分大小寫）。
4. 命中一張就回圖：有 `file_id` 快取就用它重送；沒有就從 `IImageStore` 讀檔上傳，再把 Telegram 回的 `file_id` 存起來。命中多張回清單，零張回「找不到」。
5. 所有對 Telegram 的送出都經過 `IBotMessenger`，它只有四個方法，所以 handler 可以用替身測試。

## 一次新增的流程

兩條路，驗證規則共用：

- **Telegram**：照片或檔案加 caption → `HandleImageAsync` → 下載 → `ImageFileRules` 檢查副檔名、大小、檔頭 → `KeywordNormalizer` 整理 caption → 寫資料庫、存檔 → 照片的 `file_id` 直接當快取。
- **HTTP**：`POST /api/memes` multipart → 同樣的檢查 → 寫資料庫、存檔。

## 資料

- `memes`：編號、副檔名、啟用、`telegram_file_id`、時間戳。
- `meme_keywords`：編號對關鍵字，一對多。
- 圖片檔名就是 `{編號}.{副檔名}`，編號自動遞增且不重用。
- `telegram_file_id` 是快取：Telegram 送過一次的圖可以用這個 ID 重送，不必再上傳。它綁定單一 bot，換 bot 就失效，程式會自動退回上傳並換新的。

整個系統的狀態就是 `Data/` 資料夾。設定 `Backup:Directory` 後，`BackupService` 每天用 `VACUUM INTO` 拿一份資料庫快照，並把新圖片複製過去；不直接複製資料庫檔，是因為開著 WAL 時那樣可能拿到不一致的副本。

## 幾個刻意的決定

| 決定 | 為什麼 | 詳見 |
|---|---|---|
| 跑在家中設備、不上雲 | 零費用、不開 port | [ADR 0001](../adr/0001-host-on-home-device.md) |
| Long polling | 不需要公開網址 | [ADR 0002](../adr/0002-telegram-long-polling.md) |
| SQLite 加 Dapper 手寫 SQL | 規模小、作者偏好 | [ADR 0003](../adr/0003-sqlite-dapper-local-files.md) |
| 單一專案、Minimal API、一個 endpoint 一個檔案 | 十個 endpoint 不需要多層 | [ADR 0004](../adr/0004-minimal-api-vertical-slice.md) |
| 比對在記憶體做，不用 SQL LIKE | 不必處理萬用字元跳脫，規則能用純函式測 | ADR 0003 |

完整需求與規則在 [design.md](../design.md)，使用場景在 [features/](../features/)。

## 測試

- **單元**：`KeywordMatcher`、`KeywordNormalizer`、`ImageFileRules`、`IncomingMessage` 是純函式直接測；`MemeBotHandler` 用 NSubstitute 替換 repository、image store、messenger。
- **整合**：`ApiFactory` 用 `WebApplicationFactory` 跑真的 Program，資料庫與圖片指到臨時資料夾，並拿掉 polling 的 hosted service，所以不會真的連 Telegram。
- 測試名稱用三段式中文：`方法_情境_預期`，一個場景對一個測試。

提交前三道門檻：`dotnet build` 零警告、`dotnet test` 全綠、`dotnet format --verify-no-changes`。

## 想改東西時

- 加一個 API：在 `Features/Memes/` 新增一個靜態類別提供 `Map(RouteGroupBuilder)`，在 `MemeEndpoints` 登記。
- 改比對規則：只改 `Features/Search/KeywordMatcher.cs`，Telegram 與 API 都會跟著變。
- 改資料表：在 `Migrations/` 新增一個編號 SQL 檔，不要動已存在的。
- 加一個 bot 回覆：文字放 `ReplyFormatter`，邏輯放 `MemeBotHandler`，先在 `MemeBotHandlerTests` 寫場景。
