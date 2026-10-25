# MemeHelper

個人用梗圖搜尋服務。Telegram bot 用自訂關鍵字找圖，Minimal API 管理圖片與關鍵字。.NET 10，跑在家中 Linux 設備的 systemd user unit 上，Telegram 用 long polling，不開對外 port。

## 文件

- `docs/design.md`：需求、規則、資料模型、API、沿用的慣例。改功能先改這裡，再改程式。
- `docs/features/*.feature`：使用場景（Gherkin，zh-TW），純文件。新功能或改行為先補場景，xUnit 測試以場景名稱對照。
- `docs/adr/`：決策與理由。新增前先確認符合「難以逆轉、沒脈絡會覺得奇怪、真的有取捨」三條件，格式見 `.claude/skills/domain-modeling/ADR-FORMAT.md`。
- `docs/research/azure-hosting-options.md`：Azure 方案的調查，已廢案，只供日後參考。
- `old_project_sample/`：不入 git，clone 下來不會有。若存在，裡面是同機另一個 .NET 服務的程式碼，部署、systemd、DbUp、SQLite 的寫法從那裡沿用。

## 提交前的三道門檻

```bash
dotnet build                        # 必須零警告，警告即錯誤
dotnet test                         # 必須全綠
dotnet format --verify-no-changes   # 命名與 this. 等規則只有這條會檢查
```

## 慣例

- 對話與文件用台灣繁體中文；程式碼、識別字用英文。Commit 訊息是英文 type 加一句中文結論，type 限 `feat`、`fix`、`refactor`、`docs`、`test`、`config`、`chore`。內文寫為什麼，尤其是取捨。
- 設定一律透過 `IOptions<T>`。祕密只放 `appsettings.Local.json`，範本是 `appsettings.Local.json.example`。
- 資料存取用 Dapper 手寫 SQL，一律以 `CommandDefinition` 傳 cancellation token。Schema 是 `Migrations/` 下的編號 SQL 檔，DbUp 啟動時套用；已執行過的 script 不可修改，一律新增檔案。
- 一個 endpoint 一個檔案，放在 `Features/<功能>/`。搜尋比對邏輯只存在 `Features/Search`，Telegram 與 API 共用。
- 測試：xUnit、AutoFixture、NSubstitute、AwesomeAssertions。先寫失敗的測試再寫實作。細節看 `.claude/skills/dotnet-testing-*/`。
- 時間用注入的 `TimeProvider`，不直接呼叫 `DateTime.Now`。

## 絕對不要 stage 的東西

`appsettings.Local.json`（真實 bot token）、`Data/`（SQLite 與圖片）、`old_project_sample/`。三者都在 `.gitignore`，`git add -A` 前仍看一眼 `git status`。

## 執行注意

- 同一個 bot token 同時只能有一個程序 polling。在開發機跑之前，先確認設備上的服務已停，或改用測試 bot。
- 正式服務跑在 publish 出來的 `~/Apps/MemeHelper/`，與 clone 目錄分離。改了程式碼要 publish 才生效。
