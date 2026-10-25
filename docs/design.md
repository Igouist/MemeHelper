# MemeHelper 需求與設計

狀態：草稿，待核准
日期：2026-10-25

## 1. 目的

聊天當下腦中有一個情境或一句台詞，想貼一張對應的梗圖，卻要在手機相簿裡翻幾百張縮圖，等找到時話題已經過了。這個服務讓使用者在 Telegram 輸入關鍵字，幾秒內拿到那張圖，再複製貼到任何聊天軟體。

互動方式是「輸入關鍵字回清單，輸入編號回圖」。關鍵字由使用者自訂，可以是台詞，也可以是「什麼情境會用它」。

## 2. 核心場景

1. 使用者在手機（主要）或電腦上聊天，想到一個詞。
2. 切到 Telegram，傳給 bot。
3. 命中一張：bot 直接回圖。命中多張：bot 回清單，使用者再傳編號取圖。
4. 使用者長按複製圖片，切回原本的聊天 app 貼上。

整段必須在幾秒內完成，否則沒有意義。

## 3. 目標、非目標、反目標

目標
- 用自訂關鍵字找到梗圖，單人使用。
- 透過 API（第一版用 Swagger UI 操作）上傳、刪除、停用、啟用梗圖，編輯關鍵字。
- 每月費用 0。
- 程式碼簡單到一個人能維護，規模變大再調整。

非目標（第一版不做）
- 拖曳上傳的管理網頁。
- Telegram inline mode（在任何聊天室輸入 `@bot 關鍵字`）。
- GIF、影片。
- 多使用者、群組使用。
- 關鍵字清單過長時的顯示處理。
- OCR 或自動從圖片抽台詞。

反目標（做到也算失敗）
- 回圖要等超過幾秒。
- 需要開放任何對外 port 或公開站台。
- 每月產生費用。
- 為了相容 Azure 而增加抽象層。

## 4. 限制

- 宿主是家中 24 小時開機的 Linux 設備，不對外，可透過私人 VPN 連回。
- .NET 10。
- 只收靜態 jpg、png，單檔 10 MB 以內（Telegram `sendPhoto` 上限）。
- 第一版規模：數百張，上限估數千張。

## 5. 功能需求

### 5.1 Telegram 查詢

- 只回應白名單內的 Telegram user ID，其他人一律不回應。
- 收到文字訊息：
  - 純數字：先當編號查圖。查無此編號時當關鍵字搜尋。
  - 其他：當關鍵字搜尋。
- 關鍵字搜尋：
  - 輸入以空白切成多個詞，每個詞都必須命中（AND）。
  - 一個詞命中一張圖的條件：該圖任一關鍵字包含這個詞（子字串比對）。
  - 英文不分大小寫，中文純子字串，不分詞。
  - 停用中的圖不納入。
- 回應：
  - 命中 0 張：回「找不到」。
  - 命中 1 張：直接回圖。
  - 命中 2 到 20 張：回清單，每行 `【編號】關鍵字1,關鍵字2,...`，編號由小到大。
  - 命中超過 20 張：只列前 20 張，最後加一行提示再加詞縮小範圍。
- 回圖以 Telegram 照片形式送出（`sendPhoto`），方便手機長按複製。
- 第一次送某張圖時用檔案上傳，把 Telegram 回傳的 `file_id` 存進資料庫；之後同一張圖用 `file_id` 重送。

### 5.1.1 從 Telegram 傳圖新增梗圖

手機上的匯入路徑：相簿選圖分享給 bot，說明文字（caption）填關鍵字。

- 只回應白名單；其他人傳圖一律沉默。
- 收「照片」（Telegram 會壓成 JPEG，長邊最多 1280）與「檔案」（原始位元組，限 jpg、png）。
- 關鍵字來自 caption，經同一套正規化。沒有 caption 或正規化後為空：回「請在圖片的說明文字填關鍵字，用逗號或頓號隔開」，不新增。
- 驗證與 API 上傳相同：副檔名、10 MB、檔頭。失敗回對應訊息，不新增。
- 成功：下載到圖片資料夾、寫入資料庫，回「已新增【編號】關鍵字1,關鍵字2」。
- 照片形式收到的 `file_id` 直接存為快取；檔案形式的 `file_id` 不能用 `sendPhoto` 重送，所以不存，第一次查詢時再上傳。
- 下載或存檔失敗：回「上傳失敗，請稍後再試」，已建立的紀錄與檔案一併清掉。
- 一次只處理一張。相簿多選送出時每張是獨立訊息，caption 只在其中一張上，其餘會回提示。

### 5.2 管理 API

透過 HTTP API 操作，第一版的 UI 就是 Swagger UI。

- 上傳梗圖：multipart，一個檔案加一組關鍵字。回傳編號。
- 列出梗圖：預設含停用，可用關鍵字篩選。
- 取得單張梗圖的 metadata 與圖片檔。
- 新增關鍵字、移除關鍵字。
- 停用、啟用。
- 刪除：移除圖片檔與資料。Telegram 那邊的 `file_id` 不需處理。

關鍵字正規化規則（上傳與新增時套用）
- 先以 `,`、`，`、`、` 三種分隔符號切開，手機中文輸入法不必切換。
- 去頭尾空白。
- 空字串忽略。
- 同一張圖內重複的關鍵字只保留一個，比對重複時英文不分大小寫。
- 儲存時保留原始大小寫。

### 5.3 既有梗圖的匯入

第一版不做批次匯入功能。使用者自行把圖片路徑和關鍵字整理成 CSV，用腳本逐一呼叫上傳 API，先試十張。

## 6. 技術決策

每項的理由與放棄的替代方案見 `docs/adr/`。

| 項目 | 決定 | ADR |
|---|---|---|
| 宿主 | 家中 Linux 設備，systemd service | 0001 |
| Telegram 接收 | long polling，不開對外 port | 0002 |
| Metadata 儲存 | SQLite，Dapper 手寫 SQL | 0003 |
| 圖片儲存 | 本機資料夾，檔名為編號加副檔名；`file_id` 快取在資料庫 | 0003 |
| 架構 | 單一 ASP.NET Core 專案，Minimal API，feature 資料夾，一個 endpoint 一個檔案 | 0004 |
| Telegram 套件 | Telegram.Bot | 0002 |
| Schema | DbUp 跑內嵌的編號 SQL 檔，啟動時套用 | 0003 |
| 設定 | `IOptions<T>`；祕密放 `appsettings.Local.json`（不入 git），附 `appsettings.Local.json.example` | 無 |
| 後台存取 | 綁 `0.0.0.0`，設備在 NAT 後面且不轉埠，所以只有區網與私人 VPN 連得到；不做登入 | 無 |
| 日誌 | 內建 logging 輸出到 console，由 systemd journal 收集 | 無 |
| 測試 | xUnit、AutoFixture、NSubstitute、AwesomeAssertions，TDD | 無 |

Azure 方案已調查完畢並放棄，內容保留在 `docs/research/azure-hosting-options.md`。

### 6.1 從舊專案沿用的慣例

同一台設備上已有另一個 .NET 服務以同樣方式運作，以下慣例直接沿用：

- `Directory.Build.props`：`net10.0`、`TreatWarningsAsErrors`、`EnforceCodeStyleInBuild`、`InvariantGlobalization`。
- `.editorconfig`：整份複製，私有欄位 `camelCase` 不加底線、偏好 `var`、file-scoped namespace。
- `Directory.Packages.props`：集中管理套件版本。
- 提交前三道門檻：`dotnet build` 零警告、`dotnet test` 全綠、`dotnet format --verify-no-changes`。
- Commit 訊息：英文 type 加一句中文結論，type 用 `feat`、`fix`、`refactor`、`docs`、`test`、`config`、`chore`。
- SQLite 連線開 WAL、`busy_timeout`、`foreign_keys`。
- Dapper 一律用 `CommandDefinition` 傳 cancellation token。
- `Program.cs` 開頭 `Directory.SetCurrentDirectory(AppContext.BaseDirectory)`，相對路徑才不會跟著啟動位置跑。
- `builder.Host.UseSystemd()`，搭配 unit 檔的 `Type=notify`。

### 6.2 部署與執行

- 第一版在開發機（Windows）做完、測試通過後才部署到設備。
- 開發期間：設備 `git pull` 後 `dotnet run` 放著跑。
- 開發完成後：`dotnet publish -c Release -o ~/Apps/MemeHelper`，註冊 systemd user unit（`Type=notify`、`Restart=always`、`loginctl enable-linger`），資料放在部署目錄的 `Data/`。
- 交付物包含 `deploy/memehelper.service` 範本與 README 的部署步驟。
- Telegram bot 為 MemeHelper 專用，用 BotFather 另建一個；token 填進 `appsettings.Local.json`。
- 任何時候只能有一個程序 polling 同一個 token。在開發機測試時，設備上的服務要先停，或改用另一個測試 bot。

## 7. 資料模型

概念模型
- Meme：編號（自動遞增整數）、副檔名、是否啟用、Telegram `file_id`（可空）、建立時間、更新時間。
- Keyword：屬於一張 Meme 的字串。一張 Meme 多個 Keyword。

SQLite 資料表（草案，實作者可調整欄位名）
- `memes`：`id`、`extension`、`is_enabled`、`telegram_file_id`、`created_at`、`updated_at`
- `meme_keywords`：`meme_id`、`keyword`，主鍵為兩者組合

Schema 以編號 SQL 檔內嵌在組件裡，啟動時由 DbUp 套用，DbUp 自己維護已套用紀錄。已執行過的 script 不可修改，改 schema 一律新增檔案。

圖片檔
- 路徑：`<ImageRoot>/<id>.<extension>`，`ImageRoot` 來自設定，預設 `Data/images`。
- 刪除梗圖時同時刪檔。

整個系統的狀態就是 `Data/` 資料夾（SQLite 檔加圖片）。備份由使用者自行處理。

## 8. 專案配置

```
MemeHelper.sln
Directory.Build.props
Directory.Packages.props
.editorconfig
src/MemeHelper.Api/
  Program.cs
  Configuration/  IOptions 類別與註冊
  Features/
    Memes/        上傳、列表、取圖、關鍵字、停用、啟用、刪除，一個 endpoint 一個檔案
    Search/       比對邏輯，Telegram 與管理 API 共用
  Telegram/       polling hosted service、訊息處理、回覆格式
  Database/       連線工廠、DbUp migrator、Dapper repository
  Migrations/     編號 SQL 檔，內嵌資源
  appsettings.json
  appsettings.Local.json.example
tests/MemeHelper.Tests/
deploy/memehelper.service
docs/
```

## 9. API 草案

| 方法 | 路徑 | 說明 |
|---|---|---|
| POST | `/api/memes` | multipart：`file`、`keywords`（逗號分隔的單一字串）。回 201 與編號 |
| GET | `/api/memes` | 列表。query：`keyword`（篩選）、`includeDisabled`（預設 true） |
| GET | `/api/memes/{id}` | 單張 metadata |
| GET | `/api/memes/{id}/image` | 圖片檔 |
| POST | `/api/memes/{id}/keywords` | 新增關鍵字（body 為字串陣列） |
| DELETE | `/api/memes/{id}/keywords/{keyword}` | 移除關鍵字 |
| POST | `/api/memes/{id}/disable` | 停用 |
| POST | `/api/memes/{id}/enable` | 啟用 |
| DELETE | `/api/memes/{id}` | 刪除 |

錯誤回應用 ProblemDetails。

## 10. 設定項目

| 設定 | 說明 |
|---|---|
| `Urls` | 綁定位址，預設 `http://0.0.0.0:5090`。寫在 `appsettings.json` 才會跟著 publish |
| `Telegram:BotToken` | bot token，放 `appsettings.Local.json` |
| `Telegram:AllowedUserIds` | 白名單，放 `appsettings.Local.json` |
| `Database:ConnectionString` | 預設 `Data Source=Data/memehelper.db` |
| `Storage:ImageRoot` | 圖片資料夾，預設 `Data/images` |
| `Search:MaxListResults` | 清單上限，預設 20 |

啟動時驗證 `BotToken` 與 `AllowedUserIds` 都有值，缺了直接啟動失敗並指名欄位。

## 11. 我做的決定，可推翻

這些是討論中沒有明確談到、由我決定的細節。

- 編號用短數字，不加前綴。
- 純數字輸入先當編號，查無此編號再當關鍵字。
- 非白名單使用者不回應任何訊息。
- 清單依編號由小到大排序。
- 上傳時同一次請求重複的關鍵字自動去重，不報錯。
- 新增已存在的關鍵字視為成功，不報錯。
- 移除不存在的關鍵字回 404。
- 第一版不做 `/start`、`/help` 以外的指令；`/start` 回一行說明。
- `docs/features/*.feature` 是純文件，不接 Reqnroll 之類的執行器；xUnit 測試以場景名稱命名，逐一對照。
- 另建一個專用 bot，不共用同機其他服務的 bot。共用在技術上可行（那邊只送訊息，不 polling），但分開比較不會互相干擾。
- 連接埠 5090，避開同機其他服務。
- 日誌不用 Serilog，內建 logging 進 journald 就夠。
- Schema 用 DbUp 而非手寫 `schema_version`，沿用舊專案已驗證過的做法。
- Telegram 傳圖只用 caption 當關鍵字，不做「先傳圖再問關鍵字」的兩段式對話，避免記每個聊天的狀態。
- 第一版上傳不驗證圖片尺寸。Telegram `sendPhoto` 另有寬高相加 10000 以內、長寬比 20 以內的限制，超過的圖片在送圖時才會失敗，屆時回一則錯誤訊息並記 log。不為此多一個影像套件。

## 12. 留給實作者

- Dapper repository 的介面切法與 SQL 寫法。
- polling 的錯誤處理與重試（Telegram.Bot 內建的 `ReceiverOptions` 足夠）。
- Swagger UI 的 multipart 上傳設定。
- 測試資料的建立方式。
- systemd unit 的細節。

## 13. 之後的許願

依使用者提出的順序排，都不在第一版範圍：
- 拖曳上傳的管理網頁。
- Telegram inline mode。
- 關鍵字清單過長時的顯示方式。
- 批次匯入 API。
